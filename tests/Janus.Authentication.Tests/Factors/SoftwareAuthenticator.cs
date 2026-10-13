using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Janus.Core;
using NSec.Cryptography;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// An authenticator in software: it holds one credential's key pair and answers the two
/// WebAuthn ceremonies with the bytes a browser would send back, an attestation object
/// of format <c>none</c> and a signed assertion (WebAuthn Level 3 sections 6.1, 6.5 and
/// 8.7).
/// </summary>
/// <remarks>
/// A fake for the tests of AUTH-FACT-014: what it gives is genuine, so the verification
/// the library runs is the one it runs for a real authenticator.
/// </remarks>
internal sealed class SoftwareAuthenticator
{
    /// <summary>The COSE identifier of ECDSA over P-256 with SHA-256.</summary>
    public const int Es256 = -7;

    /// <summary>The COSE identifier of EdDSA.</summary>
    public const int EdDsa = -8;

    /// <summary>The COSE identifier of RSASSA-PKCS1-v1_5 with SHA-256.</summary>
    public const int Rs256 = -257;

    /// <summary>The flag of a user who was present.</summary>
    public const byte UserPresent = 0x01;

    /// <summary>The flag of a user who was verified.</summary>
    public const byte UserVerified = 0x04;

    /// <summary>The flag of a credential that may be backed up.</summary>
    public const byte BackupEligible = 0x08;

    /// <summary>The flag of a credential that is backed up.</summary>
    public const byte BackedUp = 0x10;

    private const byte AttestedCredentialData = 0x40;
    private const byte ExtensionData = 0x80;

    private readonly int _algorithm;
    private readonly byte[] _privateKey;

    private SoftwareAuthenticator(int algorithm, byte[] credentialId, byte[] privateKey, byte[] publicKey)
    {
        _algorithm = algorithm;
        _privateKey = privateKey;
        CredentialId = credentialId;
        PublicKey = publicKey;
    }

    /// <summary>
    /// The identifier of the credential it holds.
    /// </summary>
    public byte[] CredentialId { get; }

    /// <summary>
    /// The public key of the credential it holds, as a COSE key.
    /// </summary>
    public byte[] PublicKey { get; }

    /// <summary>
    /// Makes an authenticator holding a new credential.
    /// </summary>
    /// <param name="algorithm">The COSE algorithm of the credential's key.</param>
    /// <param name="credentialId">The credential's identifier, or none to draw one.</param>
    /// <returns>The authenticator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">It makes no key of that algorithm.</exception>
    public static SoftwareAuthenticator Holding(int algorithm = Es256, byte[]? credentialId = null)
    {
        byte[] id = credentialId ?? RandomNumberGenerator.GetBytes(16);

        switch (algorithm)
        {
            case Es256:
            {
                using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

                return Holding(key, id);
            }

            case Rs256:
            {
                using var key = RSA.Create(2048);
                RSAParameters parameters = key.ExportParameters(includePrivateParameters: false);

                return new SoftwareAuthenticator(
                    algorithm,
                    id,
                    key.ExportPkcs8PrivateKey(),
                    Key(writer =>
                    {
                        writer.WriteStartMap(4);
                        Member(writer, 1, 3);
                        Member(writer, 3, Rs256);
                        Member(writer, -1, parameters.Modulus!);
                        Member(writer, -2, parameters.Exponent!);
                        writer.WriteEndMap();
                    }));
            }

            case EdDsa:
            {
                using var key = NSec.Cryptography.Key.Create(
                    SignatureAlgorithm.Ed25519,
                    new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
                byte[] point = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);

                return new SoftwareAuthenticator(
                    algorithm,
                    id,
                    key.Export(KeyBlobFormat.RawPrivateKey),
                    Key(writer =>
                    {
                        writer.WriteStartMap(4);
                        Member(writer, 1, 1);
                        Member(writer, 3, EdDsa);
                        Member(writer, -1, 6);
                        Member(writer, -2, point);
                        writer.WriteEndMap();
                    }));
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, message: null);
        }
    }

    /// <summary>
    /// Makes an authenticator holding a credential whose key pair is the given one.
    /// </summary>
    /// <param name="key">The key pair, over P-256.</param>
    /// <param name="credentialId">The credential's identifier.</param>
    /// <returns>The authenticator.</returns>
    public static SoftwareAuthenticator Holding(ECDsa key, byte[] credentialId)
    {
        ECParameters parameters = key.ExportParameters(includePrivateParameters: false);

        return new SoftwareAuthenticator(
            Es256,
            credentialId,
            key.ExportPkcs8PrivateKey(),
            Key(writer =>
            {
                writer.WriteStartMap(5);
                Member(writer, 1, 2);
                Member(writer, 3, Es256);
                Member(writer, -1, 1);
                Member(writer, -2, parameters.Q.X!);
                Member(writer, -3, parameters.Q.Y!);
                writer.WriteEndMap();
            }));
    }

    /// <summary>
    /// Answers a creation ceremony.
    /// </summary>
    /// <param name="challenge">The challenge the ceremony was opened with.</param>
    /// <param name="origin">The origin of the page that ran the ceremony.</param>
    /// <param name="relyingParty">The relying party the credential is created for.</param>
    /// <param name="flags">The flags of the authenticator data, beside the attested one.</param>
    /// <param name="counter">The signature counter.</param>
    /// <param name="crossOrigin">What the client data says of its frame, where it says.</param>
    /// <param name="extensions">Whether the authenticator adds an extension output nobody asked for.</param>
    /// <param name="transports">The transports the browser reports.</param>
    /// <returns>What the browser sends back.</returns>
    public AuthenticatorAttestation Attested(
        string challenge,
        string origin,
        string relyingParty,
        byte flags = UserPresent | UserVerified,
        uint counter = 0,
        bool? crossOrigin = null,
        bool extensions = false,
        IReadOnlyList<string>? transports = null)
    {
        byte[] credential =
        [
            .. new byte[16],
            (byte)(CredentialId.Length >> 8),
            (byte)CredentialId.Length,
            .. CredentialId,
            .. PublicKey,
        ];
        byte[] authenticatorData = AuthenticatorData(
            relyingParty,
            (byte)(flags | AttestedCredentialData),
            counter,
            credential,
            extensions);

        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);

        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString("none");
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteTextString("authData");
        writer.WriteByteString(authenticatorData);
        writer.WriteEndMap();

        string id = Base64Url.EncodeToString(CredentialId);

        return new AuthenticatorAttestation(
            id,
            id,
            "public-key",
            Base64Url.EncodeToString(ClientData("webauthn.create", challenge, origin, crossOrigin)),
            Base64Url.EncodeToString(writer.Encode()),
            transports ?? []);
    }

    /// <summary>
    /// Answers a sign-in ceremony.
    /// </summary>
    /// <param name="challenge">The challenge the ceremony was opened with.</param>
    /// <param name="origin">The origin of the page that ran the ceremony.</param>
    /// <param name="relyingParty">The relying party the credential is held for.</param>
    /// <param name="flags">The flags of the authenticator data.</param>
    /// <param name="counter">The signature counter.</param>
    /// <param name="crossOrigin">What the client data says of its frame, where it says.</param>
    /// <param name="extensions">Whether the authenticator adds an extension output nobody asked for.</param>
    /// <param name="userHandle">The user handle the authenticator returns, base64url, where it keeps one.</param>
    /// <returns>What the browser sends back.</returns>
    public AuthenticatorAssertion Asserted(
        string challenge,
        string origin,
        string relyingParty,
        byte flags = UserPresent | UserVerified,
        uint counter = 0,
        bool? crossOrigin = null,
        bool extensions = false,
        string? userHandle = null)
    {
        byte[] clientData = ClientData("webauthn.get", challenge, origin, crossOrigin);
        byte[] authenticatorData = AuthenticatorData(relyingParty, flags, counter, [], extensions);
        string id = Base64Url.EncodeToString(CredentialId);

        return new AuthenticatorAssertion(
            id,
            id,
            "public-key",
            Base64Url.EncodeToString(clientData),
            Base64Url.EncodeToString(authenticatorData),
            Base64Url.EncodeToString(Signed([.. authenticatorData, .. SHA256.HashData(clientData)])),
            userHandle);
    }

    /// <summary>
    /// Writes a creation's answer as <c>PublicKeyCredential.toJSON()</c> gives it, the
    /// <c>RegistrationResponseJSON</c> of WebAuthn Level 3 section 5.1, with the members
    /// a browser adds that the library does not read.
    /// </summary>
    /// <param name="answered">What the browser sends back.</param>
    /// <returns>The JSON object.</returns>
    public static JsonObject Written(AuthenticatorAttestation answered)
    {
        var transports = new JsonArray();

        foreach (string transport in answered.Transports)
        {
            transports.Add(transport);
        }

        return new JsonObject
        {
            ["id"] = answered.Id,
            ["rawId"] = answered.RawId,
            ["type"] = answered.Type,
            ["authenticatorAttachment"] = "platform",
            ["response"] = new JsonObject
            {
                ["clientDataJSON"] = answered.ClientDataJson,
                ["attestationObject"] = answered.AttestationObject,
                ["transports"] = transports,
                ["publicKeyAlgorithm"] = -7,
            },
            ["clientExtensionResults"] = new JsonObject { ["credProps"] = new JsonObject { ["rk"] = true } },
        };
    }

    /// <summary>
    /// Writes an assertion as <c>PublicKeyCredential.toJSON()</c> gives it, the
    /// <c>AuthenticationResponseJSON</c> of WebAuthn Level 3 section 5.1, with the
    /// members a browser adds that the library does not read.
    /// </summary>
    /// <param name="answered">What the browser sends back.</param>
    /// <returns>The JSON object.</returns>
    public static JsonObject Written(AuthenticatorAssertion answered) =>
        new()
        {
            ["id"] = answered.Id,
            ["rawId"] = answered.RawId,
            ["type"] = answered.Type,
            ["authenticatorAttachment"] = "platform",
            ["response"] = new JsonObject
            {
                ["clientDataJSON"] = answered.ClientDataJson,
                ["authenticatorData"] = answered.AuthenticatorData,
                ["signature"] = answered.Signature,
                ["userHandle"] = answered.UserHandle,
            },
            ["clientExtensionResults"] = new JsonObject { ["appid"] = false },
        };

    private static byte[] ClientData(string type, string challenge, string origin, bool? crossOrigin)
    {
        var members = new Dictionary<string, object>(capacity: 4, StringComparer.Ordinal)
        {
            ["type"] = type,
            ["challenge"] = challenge,
            ["origin"] = origin,
        };

        if (crossOrigin is bool said)
        {
            members["crossOrigin"] = said;
        }

        return JsonSerializer.SerializeToUtf8Bytes(members);
    }

    private static byte[] AuthenticatorData(
        string relyingParty,
        byte flags,
        uint counter,
        byte[] credential,
        bool extensions)
    {
        byte[] head = new byte[37];

        SHA256.HashData(Encoding.UTF8.GetBytes(relyingParty)).CopyTo(head, 0);
        head[32] = extensions ? (byte)(flags | ExtensionData) : flags;
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(33), counter);

        if (!extensions)
        {
            return [.. head, .. credential];
        }

        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);

        writer.WriteStartMap(1);
        writer.WriteTextString("credProtect");
        writer.WriteInt32(1);
        writer.WriteEndMap();

        return [.. head, .. credential, .. writer.Encode()];
    }

    private static byte[] Key(Action<CborWriter> members)
    {
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);

        members(writer);

        return writer.Encode();
    }

    private static void Member(CborWriter writer, int label, int value)
    {
        writer.WriteInt32(label);
        writer.WriteInt32(value);
    }

    private static void Member(CborWriter writer, int label, byte[] value)
    {
        writer.WriteInt32(label);
        writer.WriteByteString(value);
    }

    private byte[] Signed(byte[] data)
    {
        switch (_algorithm)
        {
            case Es256:
            {
                using var key = ECDsa.Create();

                key.ImportPkcs8PrivateKey(_privateKey, out _);

                return key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            }

            case Rs256:
            {
                using var key = RSA.Create();

                key.ImportPkcs8PrivateKey(_privateKey, out _);

                return key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }

            default:
            {
                using var key = NSec.Cryptography.Key.Import(
                    SignatureAlgorithm.Ed25519,
                    _privateKey,
                    KeyBlobFormat.RawPrivateKey);

                return SignatureAlgorithm.Ed25519.Sign(key, data);
            }
        }
    }
}
