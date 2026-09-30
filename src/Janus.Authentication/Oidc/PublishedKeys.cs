using System;
using System.Security.Cryptography;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Authentication.Oidc;

/// <summary>
/// One public signing key as a validator reads it.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. The set the deployment publishes and the
/// set a token is validated against are made from the same public keys, here, and carry
/// no private material either way.
/// </remarks>
internal static class PublishedKeys
{
    /// <summary>
    /// Reads one published key into the shape the key set and the validator both take.
    /// </summary>
    /// <param name="key">What the deployment published.</param>
    /// <returns>The key, public part only.</returns>
    /// <exception cref="ArgumentNullException">The key is absent.</exception>
    public static JsonWebKey Of(PublishedSigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Of(key.KeyId, key.Algorithm, key.PublicKey.Span);
    }

    /// <summary>
    /// Reads one stored key's public key into the shape the validator takes, held apart
    /// from any private key object made for the same key.
    /// </summary>
    /// <param name="key">The stored key.</param>
    /// <returns>The key, public part only.</returns>
    /// <exception cref="ArgumentNullException">The key is absent.</exception>
    public static JsonWebKey Of(SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Of(key.KeyId, key.Algorithm, key.PublicKey);
    }

    private static JsonWebKey Of(string keyId, string algorithm, ReadOnlySpan<byte> publicKey)
    {
        using var ecdsa = ECDsa.Create();

        ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);

        ECParameters parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        // The key carries its own coordinates and nothing else: a key built around a
        // live one would outlive it, and what validates a token would then be a
        // handle on something already disposed.
        return new JsonWebKey
        {
            Kty = JsonWebAlgorithmsKeyTypes.EllipticCurve,
            Crv = Curve(algorithm),
            X = Base64UrlEncoder.Encode(parameters.Q.X),
            Y = Base64UrlEncoder.Encode(parameters.Q.Y),
            KeyId = keyId,
            Use = JsonWebKeyUseNames.Sig,
            Alg = algorithm,
        };
    }

    // ES256 is the one algorithm the deployment signs with (chapter 10 section 4.9),
    // and P-256 is the curve it names.
    private static string Curve(string algorithm) =>
        string.Equals(algorithm, SecurityAlgorithms.EcdsaSha256, StringComparison.Ordinal)
            ? JsonWebKeyECTypes.P256
            : throw new InvalidOperationException("The signing algorithm names no curve this version holds.");
}
