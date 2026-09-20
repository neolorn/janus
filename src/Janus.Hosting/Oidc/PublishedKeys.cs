using System;
using System.Security.Cryptography;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Oidc;

/// <summary>
/// One published key as a validator reads it.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. The set the deployment publishes and the
/// set a token is validated against are the same keys, so they are built here once and
/// carry no private material either way.
/// </remarks>
internal static class PublishedKeys
{
    /// <summary>
    /// Reads one published key into the shape the key set and the validator both take.
    /// </summary>
    /// <param name="key">What the deployment published.</param>
    /// <returns>The key, public part only.</returns>
    public static JsonWebKey Of(PublishedSigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        using var ecdsa = ECDsa.Create();

        ecdsa.ImportSubjectPublicKeyInfo(key.PublicKey.Span, out _);

        ECParameters parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        // The key carries its own coordinates and nothing else: a key built around a
        // live one would outlive it, and what validates a token would then be a
        // handle on something already disposed.
        return new JsonWebKey
        {
            Kty = JsonWebAlgorithmsKeyTypes.EllipticCurve,
            Crv = Curve(key.Algorithm),
            X = Base64UrlEncoder.Encode(parameters.Q.X),
            Y = Base64UrlEncoder.Encode(parameters.Q.Y),
            KeyId = key.KeyId,
            Use = JsonWebKeyUseNames.Sig,
            Alg = key.Algorithm,
        };
    }

    // ES256 is the one algorithm the deployment signs with (chapter 10 section 4.9),
    // and P-256 is the curve it names.
    private static string Curve(string algorithm) =>
        string.Equals(algorithm, SecurityAlgorithms.EcdsaSha256, StringComparison.Ordinal)
            ? JsonWebKeyECTypes.P256
            : throw new InvalidOperationException("The signing algorithm names no curve this version holds.");
}
