using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage;

/// <summary>
/// The lock a decision on a value that no one row holds is made under: whether an
/// identifier's value is held or reserved, and whether a username is taken or held.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003, REG-SESS-005, REG-IDENT-009 and OPS-SEC-003. The lock is
/// a transaction-scoped advisory lock for each fingerprint key version the process
/// holds, so processes on either side of a fingerprint key rotation meet on a version
/// they share, and it is released with the transaction, so a crashed operation leaves
/// nothing to clear.
/// </remarks>
internal static class ValueLock
{
    /// <summary>
    /// The name the lock on an email address or a phone number is keyed under.
    /// </summary>
    public const string Identifier = "identifier";

    /// <summary>
    /// The name the lock on a username is keyed under.
    /// </summary>
    public const string Username = "username";

    /// <summary>
    /// The name the lock on a value of a kind is keyed under.
    /// </summary>
    /// <param name="kind">Which kind the value is.</param>
    /// <returns>The lock's name.</returns>
    public static string NameOf(IdentifierKind kind) =>
        kind is IdentifierKind.Username ? Username : Identifier;

    /// <summary>
    /// The key of the lock on a value under one version of the fingerprint key: the
    /// first eight bytes, read as a signed big-endian integer, of SHA-256 over the
    /// lock's name in UTF-8, one zero byte and the value's fingerprint under that
    /// version.
    /// </summary>
    /// <param name="name">The lock's name.</param>
    /// <param name="fingerprint">The value's fingerprint under the version.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException">The name is absent.</exception>
    public static long Key(string name, ReadOnlySpan<byte> fingerprint)
    {
        ArgumentNullException.ThrowIfNull(name);

        int named = Encoding.UTF8.GetByteCount(name);
        Span<byte> hashed = stackalloc byte[named + 1 + fingerprint.Length];

        _ = Encoding.UTF8.GetBytes(name, hashed);
        hashed[named] = 0;
        fingerprint.CopyTo(hashed[(named + 1)..]);

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];

        _ = SHA256.HashData(hashed, digest);

        return BinaryPrimitives.ReadInt64BigEndian(digest);
    }

    /// <summary>
    /// The keys of the lock on a value, one to each version of the fingerprint key
    /// held.
    /// </summary>
    /// <param name="name">The lock's name.</param>
    /// <param name="canonical">The value in its canonical form, as UTF-8 bytes.</param>
    /// <param name="keys">The versions of the fingerprint key.</param>
    /// <returns>The keys.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static IReadOnlyList<long> Keys(string name, ReadOnlySpan<byte> canonical, FingerprintKeys keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var locked = new List<long>(keys.Versions.Count);

        foreach (ReadOnlyMemory<byte> version in keys.Versions.Values)
        {
            locked.Add(Key(name, Fingerprint.Compute(canonical, version.Span)));
        }

        return locked;
    }

    /// <summary>
    /// The keys of the locks on several values, under every version of the fingerprint
    /// key the ring lends.
    /// </summary>
    /// <param name="values">The values, each with its kind and its canonical form.</param>
    /// <param name="ring">The key ring the fingerprint key is borrowed from.</param>
    /// <returns>The keys.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <exception cref="InvalidOperationException">The ring holds no fingerprint key.</exception>
    public static IReadOnlyList<long> Keys(
        IReadOnlyList<(IdentifierKind Kind, string Canonical)> values,
        IKeyRing ring)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(ring);

        return ring
            .BorrowFingerprintKeys(keys =>
            {
                var locked = new List<long>(values.Count * keys.Versions.Count);

                foreach ((IdentifierKind kind, string canonical) in values)
                {
                    locked.AddRange(Keys(NameOf(kind), Encoding.UTF8.GetBytes(canonical), keys));
                }

                return locked;
            })
            .Match(locked => locked, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    /// <summary>
    /// Takes every lock an operation takes on its values, in one ascending order of
    /// their keys, held until the operation's transaction ends.
    /// </summary>
    /// <param name="context">The context the operation's transaction is open on.</param>
    /// <param name="keys">The keys of every value lock the operation takes.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking them.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public static async ValueTask TakeAsync(
        StoreContext context,
        IReadOnlyList<long> keys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(keys);

        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A value is locked only inside the operation's transaction.");
        }

        // One order for every operation, so two that lock the same values never wait on
        // each other.
        foreach (long key in keys.Distinct().Order())
        {
            _ = await context.Database
                .ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
