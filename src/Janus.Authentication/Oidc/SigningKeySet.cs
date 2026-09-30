using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Authentication.Oidc;

/// <summary>
/// The signing keys as one set, which a change replaces whole and never edits: the next
/// key once made, the current key, which alone signs, each replaced key within its
/// overlap, and each retired key whose public key is still kept.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-001 and CONV-CODE-007. A set that replaces another carries the
/// objects already made for each key it keeps, makes a private key object only for the
/// next and the current key, and leaves the caller to dispose the private key object of
/// a key the new set holds without it or no longer holds.
/// </remarks>
[NeverLogged]
internal sealed class SigningKeySet
{
    private readonly IReadOnlyList<SigningKey> _stored;

    private SigningKeySet(IReadOnlyList<HeldSigningKey> keys, HeldSigningKey current)
    {
        Keys = keys;
        Current = current;
        _stored = [.. keys.Select(held => held.Key)];
    }

    /// <summary>Every key in the set.</summary>
    public IReadOnlyList<HeldSigningKey> Keys { get; }

    /// <summary>The key that signs.</summary>
    public HeldSigningKey Current { get; }

    /// <summary>
    /// What the provider signs with now.
    /// </summary>
    public SigningCredentials Signing =>
        Current.Credentials
            ?? throw new InvalidOperationException("The current signing key holds no private key object.");

    /// <summary>
    /// Builds the set that replaces another from the keys the database holds.
    /// </summary>
    /// <param name="replaced">The set it replaces, or nothing at the first read.</param>
    /// <param name="stored">The keys the database holds.</param>
    /// <param name="keys">Where a private key is read for a key that has no object yet.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">The stored keys or the reader is absent.</exception>
    /// <exception cref="InvalidOperationException">No key is current, or a key that signs has no private key.</exception>
    public static async ValueTask<SigningKeySet> ReplacingAsync(
        SigningKeySet? replaced,
        IReadOnlyList<SigningKey> stored,
        SigningKeys keys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(keys);

        var held = new List<HeldSigningKey>(stored.Count);

        try
        {
            foreach (SigningKey key in stored)
            {
                HeldSigningKey? kept = replaced?.Keys.SingleOrDefault(
                    candidate => string.Equals(candidate.Key.KeyId, key.KeyId, StringComparison.Ordinal));

                if (kept is not null && (kept.Credentials is not null || !(key.IsNext || key.IsCurrent)))
                {
                    held.Add(kept.Carried(key));
                }
                else if (key.HoldsPrivateKey && (key.IsNext || key.IsCurrent))
                {
                    held.Add(HeldSigningKey.Signing(
                        key,
                        await ImportedAsync(key, keys, cancellationToken).ConfigureAwait(false)));
                }
                else
                {
                    held.Add(HeldSigningKey.Public(key));
                }
            }
        }
        catch
        {
            // What this set made and no earlier set holds goes with it.
            foreach (HeldSigningKey made in held.Where(made => !Holds(replaced, made)))
            {
                made.DisposePrivateKey();
            }

            throw;
        }

        HeldSigningKey current = held.SingleOrDefault(key => key.Key.IsCurrent)
            ?? throw new InvalidOperationException("The database holds no current signing key.");

        return new SigningKeySet(held, current);
    }

    /// <summary>
    /// The keys the key set publishes: the next key, the current key and each replaced
    /// key within its overlap (AUTH-KEY-001).
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>The published keys.</returns>
    public IReadOnlyList<HeldSigningKey> Published(DateTimeOffset now) =>
        [.. Keys.Where(held => held.Key.IsPublished(now))];

    /// <summary>
    /// Whether a change is due to the keys this set carries.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cadence">How often a key is replaced.</param>
    /// <returns>Whether the stored keys are to be read and changed.</returns>
    public bool IsChangeDue(DateTimeOffset now, TimeSpan cadence) =>
        SigningKeys.IsChangeDue(_stored, now, cadence);

    /// <summary>
    /// Disposes the private key object of every key this set holds that the set
    /// replacing it holds without it or no longer holds.
    /// </summary>
    /// <param name="replacing">The set that replaces this one.</param>
    /// <exception cref="ArgumentNullException">The replacing set is absent.</exception>
    public void Release(SigningKeySet replacing)
    {
        ArgumentNullException.ThrowIfNull(replacing);

        foreach (HeldSigningKey held in Keys.Where(held => !Holds(replacing, held)))
        {
            held.DisposePrivateKey();
        }
    }

    /// <summary>
    /// Disposes every private key object this set holds, when the process stops.
    /// </summary>
    public void Clear()
    {
        foreach (HeldSigningKey held in Keys)
        {
            held.DisposePrivateKey();
        }
    }

    private static bool Holds(SigningKeySet? set, HeldSigningKey held) =>
        set is not null && set.Keys.Any(held.SharesPrivateKey);

    // CONV-CODE-007: the library's copy of the private key is cleared as soon as the
    // object is made from it.
    private static async ValueTask<ECDsa> ImportedAsync(
        SigningKey key,
        SigningKeys keys,
        CancellationToken cancellationToken)
    {
        byte[] material = await keys.PrivateKeyAsync(key.KeyId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A signing key that signs has no private key in the database.");

        var imported = ECDsa.Create();

        try
        {
            imported.ImportPkcs8PrivateKey(material, out _);
        }
        catch
        {
            imported.Dispose();

            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }

        return imported;
    }
}
