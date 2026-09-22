using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a browser sends back from a WebAuthn ceremony, read and checked: the client
/// data it signed over, the authenticator data it asserted, and the signature over
/// the two.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-011 and AUTH-FACT-014. Nothing a caller asserts about a
/// ceremony is taken on trust: the challenge, the origin and the signature are
/// checked here, and only what survives becomes the record the enrolment and the
/// sign-in are decided from.
/// </remarks>
internal static class WebAuthnCeremonies
{
    private const string Creation = "webauthn.create";
    private const string Assertion = "webauthn.get";
    private const int HashLength = 32;
    private const int FlagsAt = 32;
    private const int CounterAt = 33;
    private const int Shortest = 37;
    private const byte UserPresent = 0x01;
    private const byte UserVerifiedFlag = 0x04;
    private const byte BackupEligibleFlag = 0x08;
    private const byte BackupStateFlag = 0x10;
    private const int Es256 = -7;
    private const int Rs256 = -257;

    /// <summary>
    /// Reads an enrolment ceremony.
    /// </summary>
    /// <param name="answered">What the browser sent back.</param>
    /// <param name="challenge">The value the ceremony was opened with.</param>
    /// <param name="origins">The origins the relying party admits.</param>
    /// <param name="relyingPartyId">The identifier in force.</param>
    /// <returns>
    /// The registration, or the refusal where the ceremony does not stand up.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static Result<WebAuthnRegistration> Created(
        AuthenticatorAttestation answered,
        string challenge,
        IReadOnlyCollection<string> origins,
        string relyingPartyId)
    {
        ArgumentNullException.ThrowIfNull(answered);

        if (Decoded(answered.CredentialId) is not { } credentialId
            || Decoded(answered.ClientDataJson) is not { } clientData
            || Decoded(answered.AuthenticatorData) is not { } authenticatorData
            || Decoded(answered.PublicKey) is not { } publicKey)
        {
            return Rejected<WebAuthnRegistration>();
        }

        Error? refusal = null;

        _ = Ceremony(clientData, Creation, challenge, origins)
            .Match(() => true, error => Withheld(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<WebAuthnRegistration>(refusal);
        }

        if (Asserts(authenticatorData, relyingPartyId) is not { } asserted)
        {
            return Rejected<WebAuthnRegistration>();
        }

        // The key has to be one this deployment can verify with before it is stored: a
        // credential whose signatures could never be checked would sign a person in on
        // the strength of nothing (AUTH-FACT-014).
        _ = Imports(publicKey, answered.Algorithm).Match(() => true, error => Withheld(error, ref refusal));

        return refusal is not null
            ? Result.Failure<WebAuthnRegistration>(refusal)
            : Result.Success(new WebAuthnRegistration(
                credentialId,
                publicKey,
                answered.Algorithm,
                relyingPartyId,
                asserted.UserVerified,
                asserted.BackupEligible,
                asserted.BackupState,
                asserted.Counter));
    }

    /// <summary>
    /// Reads a sign-in ceremony against the credential it names.
    /// </summary>
    /// <param name="answered">What the browser sent back.</param>
    /// <param name="challenge">The value the sign-in was opened with.</param>
    /// <param name="origins">The origins the relying party admits.</param>
    /// <param name="held">The credential as it was enrolled.</param>
    /// <returns>
    /// The assertion, or the refusal where the ceremony does not stand up.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static Result<WebAuthnAssertion> Asserted(
        AuthenticatorAssertion answered,
        string challenge,
        IReadOnlyCollection<string> origins,
        WebAuthnMaterial held)
    {
        ArgumentNullException.ThrowIfNull(answered);
        ArgumentNullException.ThrowIfNull(held);

        if (Decoded(answered.ClientDataJson) is not { } clientData
            || Decoded(answered.AuthenticatorData) is not { } authenticatorData
            || Decoded(answered.Signature) is not { } signature)
        {
            return Rejected<WebAuthnAssertion>();
        }

        Error? refusal = null;

        _ = Ceremony(clientData, Assertion, challenge, origins)
            .Match(() => true, error => Withheld(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<WebAuthnAssertion>(refusal);
        }

        if (Asserts(authenticatorData, held.RelyingPartyId) is not { } asserted)
        {
            return Rejected<WebAuthnAssertion>();
        }

        byte[] signed = [.. authenticatorData, .. SHA256.HashData(clientData)];

        _ = Verifies(held.PublicKey.ToArray(), held.Algorithm, signed, signature)
            .Match(() => true, error => Withheld(error, ref refusal));

        return refusal is not null
            ? Result.Failure<WebAuthnAssertion>(refusal)
            : Result.Success(new WebAuthnAssertion(
                held.CredentialId,
                held.RelyingPartyId,
                asserted.UserVerified,
                asserted.Counter,
                answered.UserHandle));
    }

    private static Result<TValue> Rejected<TValue>() =>
        Result.Failure<TValue>(Error.From(ErrorCodes.FactorRejected));

    private static bool Withheld(Error error, ref Error? refusal)
    {
        refusal = error;

        return false;
    }

    private static byte[]? Decoded(string value) =>
        value is not null && Base64Url.IsValid(value) ? Base64Url.DecodeFromChars(value) : null;

    private static Result Ceremony(
        byte[] clientData,
        string type,
        string challenge,
        IReadOnlyCollection<string> origins)
    {
        string presented;
        string answered;
        string origin;

        try
        {
            using var document = JsonDocument.Parse(clientData);

            presented = Text(document.RootElement, "type");
            answered = Text(document.RootElement, "challenge");
            origin = Text(document.RootElement, "origin");
        }
        catch (JsonException)
        {
            // Client data that does not parse is a ceremony that says nothing.
            return Result.Failure(Error.From(ErrorCodes.FactorRejected));
        }

        return string.Equals(presented, type, StringComparison.Ordinal)
            && origins.Contains(origin, StringComparer.Ordinal)
            && Matches(answered, challenge)
            ? Result.Success()
            : Result.Failure(Error.From(ErrorCodes.FactorRejected));
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()!
            : string.Empty;

    private static bool Matches(string answered, string challenge) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(answered),
            Encoding.UTF8.GetBytes(challenge));

    private static Reading? Asserts(byte[] authenticatorData, string relyingPartyId)
    {
        if (authenticatorData.Length < Shortest)
        {
            return null;
        }

        byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId));

        if (!CryptographicOperations.FixedTimeEquals(authenticatorData.AsSpan(0, HashLength), expected))
        {
            return null;
        }

        byte flags = authenticatorData[FlagsAt];

        return (flags & UserPresent) == 0
            ? null
            : new Reading(
                (flags & UserVerifiedFlag) != 0,
                (flags & BackupEligibleFlag) != 0,
                (flags & BackupStateFlag) != 0,
                BinaryPrimitives.ReadUInt32BigEndian(authenticatorData.AsSpan(CounterAt, sizeof(uint))));
    }

    // EdDSA stands in the allow-list of chapter 10 section 4 and has no verifier in
    // the base class library on this runtime, so a credential offering it is refused
    // here rather than enrolled unverifiable.
    private static Result Imports(byte[] publicKey, int algorithm)
    {
        try
        {
            switch (algorithm)
            {
                case Es256:
                    using (var key = ECDsa.Create())
                    {
                        key.ImportSubjectPublicKeyInfo(publicKey, out _);
                    }

                    return Result.Success();

                case Rs256:
                    using (var key = RSA.Create())
                    {
                        key.ImportSubjectPublicKeyInfo(publicKey, out _);
                    }

                    return Result.Success();

                default:
                    return Result.Failure(Error.From(ErrorCodes.WebAuthnAlgorithmNotAllowed));
            }
        }
        catch (CryptographicException)
        {
            // A key the runtime will not read is a key no signature could be checked
            // against.
            return Result.Failure(Error.From(ErrorCodes.WebAuthnAlgorithmNotAllowed));
        }
    }

    private static Result Verifies(
        byte[] publicKey,
        int algorithm,
        byte[] signed,
        byte[] signature)
    {
        try
        {
            switch (algorithm)
            {
                case Es256:
                    using (var key = ECDsa.Create())
                    {
                        key.ImportSubjectPublicKeyInfo(publicKey, out _);

                        return key.VerifyData(
                            signed,
                            signature,
                            HashAlgorithmName.SHA256,
                            DSASignatureFormat.Rfc3279DerSequence)
                            ? Result.Success()
                            : Result.Failure(Error.From(ErrorCodes.FactorRejected));
                    }

                case Rs256:
                    using (var key = RSA.Create())
                    {
                        key.ImportSubjectPublicKeyInfo(publicKey, out _);

                        return key.VerifyData(
                            signed,
                            signature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1)
                            ? Result.Success()
                            : Result.Failure(Error.From(ErrorCodes.FactorRejected));
                    }

                default:
                    return Result.Failure(Error.From(ErrorCodes.WebAuthnAlgorithmNotAllowed));
            }
        }
        catch (CryptographicException)
        {
            // A key or a signature the runtime will not read is a signature that does
            // not verify.
            return Result.Failure(Error.From(ErrorCodes.FactorRejected));
        }
    }

    private sealed record Reading(
        bool UserVerified,
        bool BackupEligible,
        bool BackupState,
        uint Counter);
}
