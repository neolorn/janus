using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fido2NetLib;
using Fido2NetLib.Exceptions;
using Fido2NetLib.Objects;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a browser sends back from a WebAuthn ceremony, verified: by the package the
/// deployment's conventions name for it, and then by the steps of WebAuthn Level 3
/// sections 7.1 and 7.2 that the package leaves to its caller.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-010, AUTH-FACT-011 and AUTH-FACT-014 (D-197). Nothing a caller
/// asserts about a ceremony is taken on trust: only what the verification returns
/// becomes the record the enrolment and the sign-in are decided from. The library asks
/// for no extension, so the client extension outputs are never handed to the package
/// and an authenticator's own are never read.
/// </remarks>
internal static class WebAuthnCeremonies
{
    private const string PublicKeyType = "public-key";

    private const long AlgorithmLabel = 3;

    /// <summary>
    /// Verifies an enrolment ceremony.
    /// </summary>
    /// <param name="answered">What the browser sent back.</param>
    /// <param name="challenge">The value the ceremony was opened with.</param>
    /// <param name="party">The relying party in force.</param>
    /// <param name="unheld">
    /// Whether a credential identifier is one no credential holds yet.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The registration, or the refusal where the ceremony does not stand up.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async ValueTask<Result<WebAuthnRegistration>> CreatedAsync(
        AuthenticatorAttestation answered,
        string challenge,
        RelyingParty party,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<bool>> unheld,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(answered);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(unheld);

        if (Decoded(answered.RawId) is not { } rawId
            || Decoded(answered.ClientDataJson) is not { } clientData
            || Decoded(answered.AttestationObject) is not { } attestationObject
            || Decoded(challenge) is not { } expected
            || answered.Id is null)
        {
            return Rejected<WebAuthnRegistration>();
        }

        var response = new AuthenticatorAttestationRawResponse
        {
            Id = answered.Id,
            RawId = rawId,
            Type = Typed(answered.Type),
            Response = new AuthenticatorAttestationRawResponse.AttestationResponse
            {
                ClientDataJson = clientData,
                AttestationObject = attestationObject,
            },
        };

        // The user is no part of what the verification judges: it reads the relying
        // party, the challenge, the algorithms and the user verification asked for.
        var options = new CredentialCreateOptions
        {
            Rp = new PublicKeyCredentialRpEntity(party.Id, party.Id),
            User = new Fido2User { Id = [], Name = string.Empty, DisplayName = string.Empty },
            Challenge = expected,
            PubKeyCredParams = [.. party.Algorithms.Select(algorithm => new PubKeyCredParam((COSE.Algorithm)algorithm))],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                UserVerification = UserVerificationRequirement.Required,
            },
            Attestation = AttestationConveyancePreference.None,
        };

        RegisteredPublicKeyCredential made;

        try
        {
            made = await Package(party, party.Id)
                .MakeNewCredentialAsync(
                    new MakeNewCredentialParams
                    {
                        AttestationResponse = response,
                        OriginalOptions = options,
                        IsCredentialIdUniqueToUserCallback = async (asked, token) =>
                            await unheld(asked.CredentialId, token).ConfigureAwait(false),
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Fido2VerificationException refused)
        {
            return Result.Failure<WebAuthnRegistration>(Refusal(refused));
        }

        if (Embedded(clientData))
        {
            return Rejected<WebAuthnRegistration>();
        }

        return Algorithm(made.PublicKey, party) is int algorithm
            ? Result.Success(new WebAuthnRegistration(
                made.Id,
                made.PublicKey,
                algorithm,
                party.Id,
                UserVerified: true,
                made.IsBackupEligible,
                made.IsBackedUp,
                made.SignCount,
                [.. answered.Transports ?? []]))
            : Result.Failure<WebAuthnRegistration>(Error.From(ErrorCodes.WebAuthnAlgorithmNotAllowed));
    }

    /// <summary>
    /// Verifies a sign-in ceremony against the credential it names.
    /// </summary>
    /// <param name="answered">What the browser sent back.</param>
    /// <param name="challenge">The value the sign-in was opened with.</param>
    /// <param name="party">The relying party in force.</param>
    /// <param name="held">The credential as it was enrolled.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The assertion, or the refusal where the ceremony does not stand up.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async ValueTask<Result<WebAuthnAssertion>> AssertedAsync(
        AuthenticatorAssertion answered,
        string challenge,
        RelyingParty party,
        WebAuthnMaterial held,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(answered);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(held);

        if (Decoded(answered.RawId) is not { } rawId
            || Decoded(answered.ClientDataJson) is not { } clientData
            || Decoded(answered.AuthenticatorData) is not { } authenticatorData
            || Decoded(answered.Signature) is not { } signature
            || Decoded(challenge) is not { } expected
            || answered.Id is null)
        {
            return Rejected<WebAuthnAssertion>();
        }

        // The signature is verified under the algorithm of the key the credential holds,
        // which is the one it was enrolled with or the record is not one an enrolment
        // left. A record whose key does not read is a signature that does not verify.
        Error? unkept = Kept(held.PublicKey).Match<Error?>(
            algorithm => algorithm == held.Algorithm ? null : Error.From(ErrorCodes.WebAuthnAlgorithmNotAllowed),
            error => error);

        if (unkept is not null)
        {
            return Result.Failure<WebAuthnAssertion>(unkept);
        }

        byte[]? handle = null;

        if (answered.UserHandle is not null)
        {
            handle = Decoded(answered.UserHandle);

            if (handle is null)
            {
                return Rejected<WebAuthnAssertion>();
            }
        }

        var response = new AuthenticatorAssertionRawResponse
        {
            Id = answered.Id,
            RawId = rawId,
            Type = Typed(answered.Type),
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                ClientDataJson = clientData,
                AuthenticatorData = authenticatorData,
                Signature = signature,
                UserHandle = handle,
            },
        };

        // AUTH-FACT-011: the ceremony is verified under the identifier the credential
        // was enrolled under, so one that no longer binds is told apart afterwards from
        // an assertion that does not stand up.
        var options = new AssertionOptions
        {
            Challenge = expected,
            RpId = held.RelyingPartyId,
            UserVerification = UserVerificationRequirement.Required,
            AllowCredentials = [new PublicKeyCredentialDescriptor(held.CredentialId.ToArray())],
        };

        VerifyAssertionResult verified;
        AuthenticatorData asserted;

        try
        {
            // The counter is judged by the library on the credential's row under its
            // lock (D-166 X3), in the form AUTH-FACT-014 states, so the package is
            // given none to judge against. Whose the handle is, is judged with the
            // credential's state, where a refusal discloses neither.
            verified = await Package(party, held.RelyingPartyId)
                .MakeAssertionAsync(
                    new MakeAssertionParams
                    {
                        AssertionResponse = response,
                        OriginalOptions = options,
                        StoredPublicKey = held.PublicKey.ToArray(),
                        StoredSignatureCounter = 0,
                        IsUserHandleOwnerOfCredentialIdCallback = (_, _) => Task.FromResult(true),
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            asserted = AuthenticatorData.Parse(authenticatorData);
        }
        catch (Fido2VerificationException refused)
        {
            return Result.Failure<WebAuthnAssertion>(Refusal(refused));
        }

        return Embedded(clientData)
            ? Rejected<WebAuthnAssertion>()
            : Result.Success(new WebAuthnAssertion(
                held.CredentialId,
                held.RelyingPartyId,
                UserVerified: true,
                verified.SignCount,
                asserted.IsBackupEligible,
                verified.IsBackedUp,
                answered.UserHandle));
    }

    private static Fido2 Package(RelyingParty party, string relyingPartyId) =>
        new(
            new Fido2Configuration
            {
                RPID = relyingPartyId,
                RPName = relyingPartyId,
                Origins = new HashSet<string>(party.Origins.Concat(party.RelatedOrigins), StringComparer.Ordinal),
            },
            metadataService: null);

    // Every refusal keeps the code it had before the package verified the ceremony; one
    // at a step the package adds is the refusal of a factor (AUTH-FACT-014, D-197).
    private static Error Refusal(Fido2VerificationException refused) =>
        Error.From(refused.Code switch
        {
            Fido2ErrorCode.CredentialAlgorithmRequirementNotMet => ErrorCodes.WebAuthnAlgorithmNotAllowed,
            Fido2ErrorCode.UserVerificationRequirementNotMet => ErrorCodes.WebAuthnUserVerificationRequired,
            _ => ErrorCodes.FactorRejected,
        });

    private static Result<TValue> Rejected<TValue>() =>
        Result.Failure<TValue>(Error.From(ErrorCodes.FactorRejected));

    private static byte[]? Decoded(string value) =>
        value is not null && Base64Url.IsValid(value) ? Base64Url.DecodeFromChars(value) : null;

    private static PublicKeyCredentialType Typed(string type) =>
        string.Equals(type, PublicKeyType, StringComparison.Ordinal)
            ? PublicKeyCredentialType.PublicKey
            : PublicKeyCredentialType.Invalid;

    // WebAuthn Level 3 sections 7.1 and 7.2: no page of the deployment is expected to
    // run a ceremony inside another site's frame, so client data that says it was is
    // refused. The package has read the client data by now, so it parses.
    private static bool Embedded(byte[] clientData)
    {
        using var document = JsonDocument.Parse(clientData);

        return document.RootElement.TryGetProperty("crossOrigin", out JsonElement crossOrigin)
            && crossOrigin.ValueKind is JsonValueKind.True;
    }

    // The algorithm a stored key is of: the value its COSE key carries under label 3
    // (RFC 9052 section 7.1), or the refusal where the bytes are no COSE key.
    private static Result<long> Kept(ReadOnlyMemory<byte> publicKey)
    {
        try
        {
            var reader = new CborReader(publicKey, CborConformanceMode.Lax);

            if (reader.PeekState() is not CborReaderState.StartMap)
            {
                return Rejected<long>();
            }

            _ = reader.ReadStartMap();

            while (reader.PeekState() is CborReaderState.UnsignedInteger or CborReaderState.NegativeInteger)
            {
                if (reader.ReadInt64() == AlgorithmLabel
                    && reader.PeekState() is CborReaderState.UnsignedInteger or CborReaderState.NegativeInteger)
                {
                    return Result.Success(reader.ReadInt64());
                }

                reader.SkipValue();
            }

            return Rejected<long>();
        }
        catch (Exception unread) when (unread is CborContentException or OverflowException)
        {
            return Result.Failure<long>(Error.From(ErrorCodes.FactorRejected));
        }
    }

    // The algorithm of the key the attested credential data carries, which the package
    // has held to the allow-list by now.
    private static int? Algorithm(byte[] publicKey, RelyingParty party)
    {
        var key = new CredentialPublicKey(publicKey);

        foreach (int algorithm in party.Algorithms)
        {
            if (key.IsSameAlg((COSE.Algorithm)algorithm))
            {
                return algorithm;
            }
        }

        return null;
    }
}
