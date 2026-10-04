using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Sending;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The sending ledger over the four tables that hold what has gone out: the counter per
/// destination, the counter per other restriction key, the credit granted to a key, and
/// the sends that count until they settle.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="connections">Where the counting statements take their connection from.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, INT-SMS-005, PRIV-RET-005, OPS-SEC-003 and CONV-DESIGN-003.
/// A destination is counted apart from every other key, so each table is swept by the
/// longest interval of its own restrictions (D-166, 122). The plain key value crosses
/// into this class and no further. What was counted or granted under a previous version
/// of the fingerprint key still stands until the rotation retires the version; what is
/// counted or granted now is under the current one. A send is judged with the counter of
/// each of its keys held, the counters taken in one order (the destinations' table
/// first, each table's keys by their hash), so two sends judged at once are judged one
/// after the other and never wait on each other in a ring (D-183).
/// </remarks>
internal sealed class SendLedger(StoreContext context, DataConnections connections, IKeyRing ring)
    : ISendLedger
{
    private const string Separator = "\u0000";

    private const string Destinations = "identity.send_counters";

    private const string Keys = "identity.send_key_counters";

    private static readonly string[] Tables = [Destinations, Keys];

    // The grant's row is held while the credit is read and spent, so a credit is spent
    // once.
    private const string Credit =
        """
        SELECT credit, fingerprint_version FROM identity.send_grants WHERE key = @key FOR UPDATE;
        """;

    private const string Spend =
        """
        UPDATE identity.send_grants SET credit = credit - 1 WHERE key = @key;
        """;

    private const string SpendLast =
        """
        DELETE FROM identity.send_grants WHERE key = @key;
        """;

    private const string Grant =
        """
        INSERT INTO identity.send_grants AS granted (key, fingerprint_version, credit)
        VALUES (@key, @version, @credit)
        ON CONFLICT (key) DO UPDATE SET credit = granted.credit + EXCLUDED.credit;
        """;

    private const string Admitted =
        """
        INSERT INTO identity.sends
            (reference, counted, spent, spent_versions, fingerprint_version, sent_at, settles_at)
        VALUES (@reference, @counted, @spent, @versions, @version, @at, @settles);
        """;

    private const string Forgotten =
        """
        DELETE FROM identity.sends WHERE reference = @reference;
        """;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<RestrictionKey, SendCounter>> HoldAsync(
        IReadOnlyCollection<RestrictionKey> keys,
        CounterStaleness stale,
        byte[]? setAside,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);

        // AUTH-ABUSE-004 AC6: a record whose newest time decides nothing holds
        // nothing, so it goes before anything is read rather than standing until the
        // key it names is sent to again.
        await SweepAsync(stale, cancellationToken).ConfigureAwait(false);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        // A send judged again is read under its own row's lock first, as a delivery
        // report's release reads it, so the two release it once between them.
        SendRecord? retried = setAside is null
            ? null
            : await CountedAsync(setAside, cancellationToken).ConfigureAwait(false);

        var candidates = keys.ToDictionary(key => key, Candidates);

        foreach ((string table, byte[] hashed, bool current) in Ordered(candidates, retried))
        {
            _ = await ambient.Connection
                .ExecuteAsync(new CommandDefinition(
                    current ? Taking(table) : Holding(table),
                    new { key = hashed, version = Fingerprint.CurrentVersion(ring) },
                    ambient.Transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        if (retried is not null)
        {
            await ReleasedAsync(ambient, retried, cancellationToken).ConfigureAwait(false);
        }

        var standing = new Dictionary<RestrictionKey, SendCounter>(keys.Count);

        foreach ((RestrictionKey key, IReadOnlyList<byte[]> hashes) in candidates)
        {
            var sent = new List<DateTimeOffset>();
            int credit = 0;

            foreach (byte[] hashed in hashes)
            {
                DateTimeOffset[]? times = await TimesAsync(key.Kind, hashed, cancellationToken)
                    .ConfigureAwait(false);

                int? granted = await context.SendGrants
                    .AsNoTracking()
                    .Where(grant => grant.Key == hashed)
                    .Select(grant => (int?)grant.Credit)
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                sent.AddRange(times ?? []);
                credit += granted ?? 0;
            }

            // The times are read oldest first, whichever version counted them.
            standing[key] = new SendCounter([.. sent.Order()], credit);
        }

        return standing;
    }

    /// <inheritdoc/>
    public async ValueTask SweepAsync(CounterStaleness stale, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stale);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Swept(Destinations),
                new { stale = stale.Destinations },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Swept(Keys),
                new { stale = stale.Keys },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(
        byte[] reference,
        IReadOnlyCollection<SendCount> counted,
        IReadOnlyCollection<RestrictionKey> spent,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(counted);
        ArgumentNullException.ThrowIfNull(spent);

        TimeSpan settles = TimeSpan.Zero;

        // The rows are written in the order they were taken in, the destinations' table
        // first and each table's keys by their hash.
        (SendCount Count, byte[] Hashed)[] ordered =
        [
            .. counted
                .Select(count => (Count: count, Hashed: Hashed(count.Key)))
                .OrderBy(one => one.Count.Key.Kind is RestrictionKeyKind.Destination ? 0 : 1)
                .ThenBy(one => Convert.ToHexString(one.Hashed), StringComparer.Ordinal),
        ];

        foreach ((SendCount count, byte[] hashed) in ordered)
        {
            await CountAsync(count, hashed, at, cancellationToken).ConfigureAwait(false);

            if (count.Retain > settles)
            {
                settles = count.Retain;
            }
        }

        var credited = new List<(byte[] Hashed, int Version)>(spent.Count);

        foreach (RestrictionKey key in spent)
        {
            foreach (byte[] hashed in Candidates(key))
            {
                if (await SpentAsync(hashed, cancellationToken).ConfigureAwait(false) is int version)
                {
                    credited.Add((hashed, version));
                    break;
                }
            }
        }

        await SweepSettledAsync(at, cancellationToken).ConfigureAwait(false);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        // The row is written by its own statement and not tracked for the commit, so a
        // release later in the same transaction finds it.
        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Admitted,
                new
                {
                    reference,
                    counted = ordered.Select(one => one.Hashed).ToArray(),
                    spent = credited.Select(one => one.Hashed).ToArray(),
                    versions = credited.Select(one => one.Version).ToArray(),
                    version = Fingerprint.CurrentVersion(ring),
                    at,
                    settles = at + settles,
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SweepSettledAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        _ = await context.Sends
            .Where(send => send.SettlesAt < now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<bool> HoldsAsync(byte[] reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return await context.Sends
            .AsNoTracking()
            .AnyAsync(send => send.Reference == reference, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ReleaseAsync(byte[] reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        // The row is keyed by the hash of the reference, so an unknown one finds
        // nothing and a settled one was swept (INT-SMS-005). D-166 X3: it is read under
        // its lock, so two releases of it at once release it once.
        SendRecord? send = await CountedAsync(reference, cancellationToken).ConfigureAwait(false);

        if (send is null)
        {
            return false;
        }

        await ReleasedAsync(ambient, send, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask GrantAsync(
        RestrictionKey key,
        int credit,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        // D-166 X3: the credit is added to the row as it stands, so two grants at once
        // add both.
        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Grant,
                new { key = Hashed(key), version = Fingerprint.CurrentVersion(ring), credit },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    // A key's times keep what its restriction still counts, and the send, written as one
    // statement over the row as it stands.
    private static string Counting(string table) =>
        $"""
        INSERT INTO {table} AS counter (key, fingerprint_version, sent_at)
        VALUES (@key, @version, ARRAY[CAST(@at AS timestamptz)])
        ON CONFLICT (key) DO UPDATE
        SET sent_at = ARRAY(
                SELECT kept.sent
                FROM unnest(counter.sent_at) WITH ORDINALITY AS kept (sent, ordinal)
                WHERE kept.sent > @since
                ORDER BY kept.ordinal)
            || CAST(@at AS timestamptz);
        """;

    // The counter of a key under the current version of the fingerprint key, created
    // where none stands and held to the end of the transaction either way: the update
    // of a row that stands changes nothing and takes its lock.
    private static string Taking(string table) =>
        $"""
        INSERT INTO {table} AS counter (key, fingerprint_version, sent_at)
        VALUES (@key, @version, CAST(ARRAY[] AS timestamptz[]))
        ON CONFLICT (key) DO UPDATE SET key = counter.key;
        """;

    // A counter that stands under a previous version of the key, or one a send being
    // judged again counted against, held where it stands and never created.
    private static string Holding(string table) =>
        $"""
        SELECT 1 FROM {table} WHERE key = @key FOR UPDATE;
        """;

    // One of a key's times taken out of the row as it stands, and the row gone with its
    // last time.
    private static string Released(string table) =>
        $"""
        UPDATE {table}
        SET sent_at = sent_at[:array_position(sent_at, CAST(@sent AS timestamptz)) - 1]
            || sent_at[array_position(sent_at, CAST(@sent AS timestamptz)) + 1:]
        WHERE key = @key AND array_position(sent_at, CAST(@sent AS timestamptz)) IS NOT NULL;
        DELETE FROM {table} WHERE key = @key AND cardinality(sent_at) = 0;
        """;

    // Every record whose newest time decides nothing, and every record that holds no
    // time: the expression is the one each table's index is over. A record another
    // transaction holds is being judged on, and is left to the next sweep rather than
    // waited for, so a sweep never waits on a judgement that waits on it.
    private static string Swept(string table) =>
        $"""
        DELETE FROM {table}
        WHERE key IN (
            SELECT key FROM {table}
            WHERE cardinality(sent_at) = 0 OR sent_at[(cardinality(sent_at) - 1) + 1] < @stale
            FOR UPDATE SKIP LOCKED);
        """;

    private static string Table(RestrictionKeyKind kind) =>
        kind is RestrictionKeyKind.Destination ? Destinations : Keys;

    private static byte[] Named(RestrictionKey key) =>
        Encoding.UTF8.GetBytes(key.Restriction + Separator + key.Value);

    // The rows a judgement takes, in the one order every judgement takes them in. A key
    // of the send is taken in its own table under every version of the fingerprint key
    // held; a hash a send being judged again counted against is kept in one table or
    // the other, so it is taken in both.
    private static IEnumerable<(string Table, byte[] Hashed, bool Current)> Ordered(
        Dictionary<RestrictionKey, IReadOnlyList<byte[]>> candidates,
        SendRecord? retried)
    {
        var taken = new List<(string Table, byte[] Hashed, bool Current)>();

        foreach ((RestrictionKey key, IReadOnlyList<byte[]> hashes) in candidates)
        {
            for (int index = 0; index < hashes.Count; index++)
            {
                taken.Add((Table(key.Kind), hashes[index], index == 0));
            }
        }

        foreach (byte[] hashed in retried?.Counted ?? [])
        {
            taken.AddRange(Tables.Select(table => (table, hashed, false)));
        }

        return taken
            .GroupBy(one => (one.Table, Hex: Convert.ToHexString(one.Hashed)))
            .Select(same => (same.Key.Table, same.First().Hashed, Current: same.Any(one => one.Current), same.Key.Hex))
            .OrderBy(one => one.Table == Destinations ? 0 : 1)
            .ThenBy(one => one.Hex, StringComparer.Ordinal)
            .Select(one => (one.Table, one.Hashed, one.Current));
    }

    // The send taken back out of every bucket it counted against, the credit it spent
    // given back where the version it was granted under is still held, and its row gone.
    private async ValueTask ReleasedAsync(AmbientConnection ambient, SendRecord send, CancellationToken cancellationToken)
    {
        // The send names its keys by their hashes alone, and a hash is kept in one
        // table or the other, so each is looked for in both, in the order a send counts
        // them. One of its times is taken out of the row as it then stands.
        foreach (string table in Tables)
        {
            foreach (byte[] hashed in send.Counted.OrderBy(Convert.ToHexString, StringComparer.Ordinal))
            {
                _ = await ambient.Connection
                    .ExecuteAsync(new CommandDefinition(
                        Released(table),
                        new { key = hashed, sent = send.SentAt },
                        ambient.Transaction,
                        cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }
        }

        for (int index = 0; index < send.Spent.Length; index++)
        {
            int version = send.SpentVersions[index];

            // Credit granted under a version the rotation has retired was forgotten with
            // it, and nothing could find it again.
            if (!Holds(version))
            {
                continue;
            }

            _ = await ambient.Connection
                .ExecuteAsync(new CommandDefinition(
                    Grant,
                    new { key = send.Spent[index], version, credit = 1 },
                    ambient.Transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Forgotten,
                new { reference = send.Reference },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private async ValueTask CountAsync(
        SendCount count,
        byte[] hashed,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Counting(Table(count.Key.Kind)),
                new { key = hashed, version = Fingerprint.CurrentVersion(ring), at, since = at - count.Retain },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    // Credit support granted under one version of the key, spent by one send with the
    // grant's row held: the version the grant stands under, or nothing where no credit
    // stands under that hash.
    private async ValueTask<int?> SpentAsync(byte[] hashed, CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        (int Credit, int Version)? granted = await ambient.Connection
            .QuerySingleOrDefaultAsync<(int Credit, int Version)?>(new CommandDefinition(
                Credit,
                new { key = hashed },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (granted is not (int standing, int version))
        {
            return null;
        }

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                standing > 1 ? Spend : SpendLast,
                new { key = hashed },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return version;
    }

    // A destination is kept in its own table and every other key in the other.
    private async ValueTask<DateTimeOffset[]?> TimesAsync(
        RestrictionKeyKind kind,
        byte[] hashed,
        CancellationToken cancellationToken) =>
        kind is RestrictionKeyKind.Destination
            ? await context.SendCounters
                .AsNoTracking()
                .Where(counter => counter.Key == hashed)
                .Select(counter => counter.SentAt)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : await context.SendKeyCounters
                .AsNoTracking()
                .Where(counter => counter.Key == hashed)
                .Select(counter => counter.SentAt)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

    // The row is keyed by the hash of the reference, so an unknown one finds nothing
    // and a settled one was swept (INT-SMS-005). D-166 X3: it is read under its lock, so
    // two releases of it at once release it once.
    private async ValueTask<SendRecord?> CountedAsync(byte[] reference, CancellationToken cancellationToken) =>
        (await context.Sends
                .FromSql($"SELECT * FROM identity.sends WHERE reference = {reference} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

    private bool Holds(int version) =>
        ring.BorrowFingerprintKeys(keys => keys.Versions.ContainsKey(version))
            .Match(held => held, error => throw new InvalidOperationException(error.Code.ToString()));

    private byte[] Hashed(RestrictionKey key) => Fingerprint.Compute(Named(key), ring);

    private IReadOnlyList<byte[]> Candidates(RestrictionKey key) =>
        Fingerprint.Candidates(Named(key), ring);
}
