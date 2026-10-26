using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Janus.Testing;

/// <summary>
/// A WebAuthn authenticator in memory that answers the library's creation and request
/// options with what a browser sends back, so a test enrols a passkey and signs in with
/// it through the library's own ceremonies.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003 and AUTH-FACT-014. It holds one ES256 credential, attests it
/// with the <c>none</c> format, and on every ceremony reports the user present and
/// verified and the credential backup eligible and backed up, with a signature counter of
/// zero, as a synced passkey reports them (WebAuthn Level 3 sections 6.1, 6.5 and 8.7).
/// What it answers is genuine, so the library verifies it as it verifies a real
/// authenticator.
/// </remarks>
public sealed class SoftwarePasskey : IDisposable
{
    // The COSE identifier of ECDSA over P-256 with SHA-256.
    private const int Es256 = -7;

    // User present, user verified, backup eligible, backed up.
    private const byte Flags = 0x01 | 0x04 | 0x08 | 0x10;

    private const byte AttestedCredentialData = 0x40;

    private readonly string _origin;

    private readonly byte[] _credentialId;

    private byte[]? _privateKey;

    private string? _userHandle;

    private bool _disposed;

    /// <summary>
    /// Makes an authenticator that runs its ceremonies on a page of an origin.
    /// </summary>
    /// <param name="origin">The origin of the page, as the browser reports it in the client data.</param>
    /// <param name="randomness">Where the credential's identifier is drawn from.</param>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public SoftwarePasskey(Uri origin, RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(randomness);

        _origin = origin.GetLeftPart(UriPartial.Authority);
        _credentialId = new byte[16];
        randomness.GetBytes(_credentialId);
    }

    /// <summary>
    /// The identifier of the credential it holds, base64url, as the library records it.
    /// </summary>
    public string CredentialId => Base64Url.EncodeToString(_credentialId);

    /// <summary>
    /// Answers the creation options of <c>POST /auth/webauthn/register/begin</c>, creating
    /// the credential.
    /// </summary>
    /// <param name="options">The options the library answered.</param>
    /// <returns>
    /// The <c>RegistrationResponseJSON</c> that <c>PublicKeyCredential.toJSON()</c> gives, for
    /// <c>credential</c> of <c>POST /auth/webauthn/register/complete</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// It holds a credential already, or the options allow no ES256 key.
    /// </exception>
    /// <exception cref="ObjectDisposedException">It has been disposed.</exception>
    public JsonObject Create(JsonElement options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_privateKey is not null)
        {
            throw new InvalidOperationException("A software passkey holds one credential.");
        }

        bool es256 = options.TryGetProperty("algorithms", out JsonElement algorithms)
            && algorithms.EnumerateArray().Any(algorithm => algorithm.GetInt32() == Es256);

        if (!es256)
        {
            throw new InvalidOperationException("The options allow no key the software passkey makes.");
        }

        string relyingParty = options.GetProperty("relyingPartyId").GetString()!;

        _userHandle = options.GetProperty("user").GetProperty("id").GetString();

        byte[] publicKey;

        using (var key = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            _privateKey = key.ExportPkcs8PrivateKey();
            publicKey = CoseKey(key.ExportParameters(includePrivateParameters: false));
        }

        byte[] credential =
        [
            .. new byte[16],
            (byte)(_credentialId.Length >> 8),
            (byte)_credentialId.Length,
            .. _credentialId,
            .. publicKey,
        ];

        var attestation = new CborWriter(CborConformanceMode.Ctap2Canonical);

        attestation.WriteStartMap(3);
        attestation.WriteTextString("fmt");
        attestation.WriteTextString("none");
        attestation.WriteTextString("attStmt");
        attestation.WriteStartMap(0);
        attestation.WriteEndMap();
        attestation.WriteTextString("authData");
        attestation.WriteByteString(AuthenticatorData(relyingParty, Flags | AttestedCredentialData, credential));
        attestation.WriteEndMap();

        return new JsonObject
        {
            ["id"] = CredentialId,
            ["rawId"] = CredentialId,
            ["type"] = "public-key",
            ["authenticatorAttachment"] = "platform",
            ["response"] = new JsonObject
            {
                ["clientDataJSON"] = Base64Url.EncodeToString(ClientData("webauthn.create", options)),
                ["attestationObject"] = Base64Url.EncodeToString(attestation.Encode()),
                ["transports"] = new JsonArray("internal", "hybrid"),
                ["publicKeyAlgorithm"] = Es256,
            },
            ["clientExtensionResults"] = new JsonObject(),
        };
    }

    /// <summary>
    /// Answers request options, those a sign-in, a factor or a step-up carries in
    /// <c>webauthn</c>, with an assertion of the credential it holds.
    /// </summary>
    /// <param name="options">The options the library answered.</param>
    /// <returns>The <c>AuthenticationResponseJSON</c> that <c>PublicKeyCredential.toJSON()</c> gives.</returns>
    /// <exception cref="InvalidOperationException">
    /// It holds no credential, or the options allow other credentials only.
    /// </exception>
    /// <exception cref="ObjectDisposedException">It has been disposed.</exception>
    public JsonObject Get(JsonElement options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_privateKey is null)
        {
            throw new InvalidOperationException("The software passkey holds no credential.");
        }

        if (options.TryGetProperty("allowCredentials", out JsonElement allowed)
            && allowed.GetArrayLength() is not 0
            && !allowed.EnumerateArray().Any(credential => credential.GetProperty("id").GetString() == CredentialId))
        {
            throw new InvalidOperationException("The options allow other credentials only.");
        }

        string relyingParty = options.GetProperty("relyingPartyId").GetString()!;

        byte[] clientData = ClientData("webauthn.get", options);
        byte[] authenticatorData = AuthenticatorData(relyingParty, Flags, []);
        byte[] signature;

        using (var key = ECDsa.Create())
        {
            key.ImportPkcs8PrivateKey(_privateKey, out _);
            signature = key.SignData(
                [.. authenticatorData, .. SHA256.HashData(clientData)],
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
        }

        return new JsonObject
        {
            ["id"] = CredentialId,
            ["rawId"] = CredentialId,
            ["type"] = "public-key",
            ["authenticatorAttachment"] = "platform",
            ["response"] = new JsonObject
            {
                ["clientDataJSON"] = Base64Url.EncodeToString(clientData),
                ["authenticatorData"] = Base64Url.EncodeToString(authenticatorData),
                ["signature"] = Base64Url.EncodeToString(signature),
                ["userHandle"] = _userHandle,
            },
            ["clientExtensionResults"] = new JsonObject(),
        };
    }

    /// <summary>
    /// Clears the private key of the credential it holds (CONV-CODE-007); it answers no
    /// ceremony afterwards.
    /// </summary>
    public void Dispose()
    {
        if (_privateKey is not null)
        {
            CryptographicOperations.ZeroMemory(_privateKey);
            _privateKey = null;
        }

        _disposed = true;
    }

    // WebAuthn Level 3 section 5.8.1: the challenge as the options carry it, base64url.
    private byte[] ClientData(string type, JsonElement options) =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["challenge"] = options.GetProperty("challenge").GetString()!,
            ["origin"] = _origin,
            ["crossOrigin"] = false,
        });

    // WebAuthn Level 3 section 6.1: the relying party's hash, the flags and the counter,
    // then the attested credential where there is one.
    private static byte[] AuthenticatorData(string relyingParty, int flags, byte[] credential)
    {
        byte[] head = new byte[37];

        SHA256.HashData(Encoding.UTF8.GetBytes(relyingParty)).CopyTo(head, 0);
        head[32] = (byte)flags;
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(33), 0);

        return [.. head, .. credential];
    }

    // RFC 9053 section 7.1.1: an EC2 key over P-256 for ES256.
    private static byte[] CoseKey(ECParameters parameters)
    {
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);

        writer.WriteStartMap(5);
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteInt32(3);
        writer.WriteInt32(Es256);
        writer.WriteInt32(-1);
        writer.WriteInt32(1);
        writer.WriteInt32(-2);
        writer.WriteByteString(parameters.Q.X!);
        writer.WriteInt32(-3);
        writer.WriteByteString(parameters.Q.Y!);
        writer.WriteEndMap();

        return writer.Encode();
    }
}
