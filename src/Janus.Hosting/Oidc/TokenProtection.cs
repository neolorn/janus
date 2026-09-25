using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the codes and the refresh tokens the server writes are encrypted under.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-002, AUTH-OIDC-002 and OPS-SEC-001. The key is derived from the
/// key-encryption key the secrets manager hands the deployment at startup, under a
/// purpose string of its own, so every instance derives the same key without holding a
/// second secret and without a key of the server's own that would die with the
/// process. Every version the deployment still holds is derived, newest first, so a
/// key rotation does not invalidate a code or a refresh token already issued.
/// <para>
/// The signing key is not this: a token is validated by relying parties against the
/// published set (AUTH-KEY-001), and nothing here is ever published.
/// </para>
/// </remarks>
internal static class TokenProtection
{
    // The purpose separates this key from every other use of the key-encryption key,
    // so that material derived here cannot unwrap a subject's data key and material
    // derived elsewhere cannot read a token.
    private const string Purpose = "identity:oidc:token-protection:v1";

    private const int Length = 32;

    /// <summary>
    /// The keys the server encrypts with and decrypts with, the current version first.
    /// </summary>
    /// <param name="keyEncryptionKeys">The versions the deployment holds.</param>
    /// <returns>The keys.</returns>
    /// <exception cref="ArgumentNullException">The versions are absent.</exception>
    public static IReadOnlyList<SymmetricSecurityKey> Keys(KeyEncryptionKeys keyEncryptionKeys)
    {
        ArgumentNullException.ThrowIfNull(keyEncryptionKeys);

        var derived = new List<SymmetricSecurityKey>(keyEncryptionKeys.Versions.Count)
        {
            Derive(keyEncryptionKeys.Current),
        };

        foreach (KeyValuePair<int, ReadOnlyMemory<byte>> version in keyEncryptionKeys.Versions)
        {
            if (version.Key != keyEncryptionKeys.CurrentVersion)
            {
                derived.Add(Derive(version.Value));
            }
        }

        return derived;
    }

    private static SymmetricSecurityKey Derive([NeverLogged] ReadOnlyMemory<byte> material)
    {
        byte[] key = new byte[Length];

        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            material.Span,
            key,
            salt: [],
            Encoding.UTF8.GetBytes(Purpose));

        return new SymmetricSecurityKey(key);
    }
}
