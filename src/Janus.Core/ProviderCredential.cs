using System;
using System.Diagnostics.CodeAnalysis;

namespace Janus.Core;

/// <summary>
/// What this application presents at a social provider's token endpoint, as the host's
/// secret source answers it for the provider by name: a static secret the provider
/// issued, or a signing credential the library mints the client secret from at each
/// exchange.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012, OPS-SEC-002 and LIB-EXT-001. A signing credential is the
/// identifier the provider knows the deployment's account by, the identifier of the key
/// it issued, and that P-256 private key in PKCS #8, so no person mints a secret and no
/// secret lapses while the deployment runs.
/// </remarks>
[NeverLogged]
public sealed class ProviderCredential
{
    private ProviderCredential(string? issuer, string? keyId, ReadOnlyMemory<byte> material)
    {
        Issuer = issuer;
        KeyId = keyId;
        Material = material;
    }

    /// <summary>
    /// Whether this is a signing credential rather than a static secret.
    /// </summary>
    public bool IsSigned => KeyId is not null;

    /// <summary>
    /// The identifier the provider knows the deployment's account by, where this is a
    /// signing credential.
    /// </summary>
    public string? Issuer { get; }

    /// <summary>
    /// The identifier of the key the provider issued, where this is a signing
    /// credential.
    /// </summary>
    public string? KeyId { get; }

    /// <summary>
    /// The static secret as its UTF-8 bytes, or the signing credential's P-256 private
    /// key in PKCS #8.
    /// </summary>
    public ReadOnlyMemory<byte> Material { get; }

    /// <summary>
    /// A static secret the provider issued.
    /// </summary>
    /// <param name="secret">The secret, as its UTF-8 bytes.</param>
    /// <returns>The credential.</returns>
    public static ProviderCredential Secret(ReadOnlyMemory<byte> secret) => new(null, null, secret);

    /// <summary>
    /// A signing credential the library mints the client secret from at each exchange.
    /// </summary>
    /// <param name="issuer">The identifier the provider knows the deployment's account by.</param>
    /// <param name="keyId">The identifier of the key the provider issued.</param>
    /// <param name="key">That P-256 private key, in PKCS #8.</param>
    /// <returns>The credential.</returns>
    /// <exception cref="ArgumentNullException">The issuer or the key identifier is absent.</exception>
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification = "D-166 names the signing credential's form Signed, and the code carries the word the specification uses.")]
    public static ProviderCredential Signed(string issuer, string keyId, ReadOnlyMemory<byte> key)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(keyId);

        return new(issuer, keyId, key);
    }
}
