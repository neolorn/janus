using System;
using System.Security.Cryptography;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Authentication.Oidc;

/// <summary>
/// One key of the set the credential source holds: the key as stored, its public key,
/// and, from the moment it may sign until it is retired, the private key object the
/// provider signs with.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-001 and CONV-CODE-007. The public key is held apart from the
/// private key object, so disposing the object at the key's retirement breaks no
/// validation.
/// </remarks>
[NeverLogged]
internal sealed class HeldSigningKey
{
    private readonly ECDsa? _private;

    private HeldSigningKey(
        SigningKey key,
        JsonWebKey publicKey,
        ECDsa? privateKey,
        SigningCredentials? credentials)
    {
        Key = key;
        PublicKey = publicKey;
        _private = privateKey;
        Credentials = credentials;
    }

    /// <summary>The key as stored.</summary>
    public SigningKey Key { get; }

    /// <summary>Its public key, which the key set publishes and validation reads.</summary>
    public JsonWebKey PublicKey { get; }

    /// <summary>
    /// What the provider signs with, made on the private key object, or nothing where no
    /// private key object is held.
    /// </summary>
    public SigningCredentials? Credentials { get; }

    /// <summary>
    /// A key held with the private key object made for it.
    /// </summary>
    /// <param name="key">The key as stored.</param>
    /// <param name="privateKey">The private key object, which this now owns.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">The key or the object is absent.</exception>
    /// <exception cref="InvalidOperationException">The key names an algorithm this version does not sign with.</exception>
    public static HeldSigningKey Signing(SigningKey key, ECDsa privateKey)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(privateKey);

        return new HeldSigningKey(
            key,
            PublishedKeys.Of(key),
            privateKey,
            new SigningCredentials(
                new ECDsaSecurityKey(privateKey) { KeyId = key.KeyId },
                SigningKeys.Signs(key.Algorithm)
                    ? SecurityAlgorithms.EcdsaSha256
                    : throw new InvalidOperationException(
                        "The signing algorithm names no signature this version writes.")));
    }

    /// <summary>
    /// A key held by its public key alone.
    /// </summary>
    /// <param name="key">The key as stored.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">The key is absent.</exception>
    public static HeldSigningKey Public(SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return new HeldSigningKey(key, PublishedKeys.Of(key), privateKey: null, credentials: null);
    }

    /// <summary>
    /// The same key, as now stored, in a set that replaces the one it was held in: its
    /// objects carried, the private key object only where the key still has a private
    /// key.
    /// </summary>
    /// <param name="key">The key as now stored.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">The key is absent.</exception>
    public HeldSigningKey Carried(SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return key.HoldsPrivateKey
            ? new HeldSigningKey(key, PublicKey, _private, Credentials)
            : new HeldSigningKey(key, PublicKey, privateKey: null, credentials: null);
    }

    /// <summary>
    /// Whether another holds the same private key object as this one.
    /// </summary>
    /// <param name="other">The other, or nothing.</param>
    /// <returns>Whether both hold the one object.</returns>
    public bool SharesPrivateKey(HeldSigningKey? other) =>
        _private is not null && ReferenceEquals(_private, other?._private);

    /// <summary>
    /// Disposes the private key object, where one is held.
    /// </summary>
    public void DisposePrivateKey() => _private?.Dispose();
}
