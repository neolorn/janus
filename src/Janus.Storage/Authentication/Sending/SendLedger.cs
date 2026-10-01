using System;
using System.Collections.Generic;
using System.Linq;
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
/// the sends a delivery report can still take back.
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
/// counted or granted now is under the current one. Sends counted, credit granted or
/// spent and sends released at the same moment each change the row as it then stands,
/// never as it was read, so none is lost (D-166 X3).
/// </remarks>
internal sealed class SendLedger(StoreContext context, DataConnections connections, IKeyRing ring)
    : ISendLedger
{
    private const string Separator = "\u0000";

    private const string Destinations = "identity.send_counters";

    private const string Keys = "identity.send_key_counters";

    // D-166 X3: the grant's row is held while the credit is read and spent, so two sends
    // at once spend two credits.
    private const string Credit =
        """
        SELECT credit FROM identity.send_grants WHERE key = @key FOR UPDATE;
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

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<RestrictionKey, SendCounter>> CountersAsync(
        IReadOnlyCollection<RestrictionKey> keys,
        CounterStaleness stale,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);

        // AUTH-ABUSE-004 AC6: a record whose newest time decides nothing holds
        // nothing, so it goes before anything is read rather than standing until the
        // key it names is sent to again.
        await SweepAsync(stale, cancellationToken).ConfigureAwait(false);

        var standing = new Dictionary<RestrictionKey, SendCounter>();

        foreach (RestrictionKey key in keys)
        {
            var sent = new List<DateTimeOffset>();
            int credit = 0;
            bool found = false;

            foreach (byte[] hashed in Candidates(key))
            {
                DateTimeOffset[]? times = await TimesAsync(key.Kind, hashed, cancellationToken)
                    .ConfigureAwait(false);

                int? granted = await context.SendGrants
                    .AsNoTracking()
                    .Where(grant => grant.Key == hashed)
                    .Select(grant => (int?)grant.Credit)
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                found |= times is not null || granted is not null;
                sent.AddRange(times ?? []);
                credit += granted ?? 0;
            }

            if (!found)
            {
                continue;
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

        // The times are written oldest first, so the newest is the last of them and
        // each table's index is over that expression.
        _ = await context.SendCounters
            .Where(counter => counter.SentAt[counter.SentAt.Length - 1] < stale.Destinations)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        _ = await context.SendKeyCounters
            .Where(counter => counter.SentAt[counter.SentAt.Length - 1] < stale.Keys)
            .ExecuteDeleteAsync(cancellationToken)
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

        // The rows are taken in one order, the destinations' table first and each table's
        // keys by their hash, so two sends counting under the same keys never wait on each
        // other in a ring.
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

        foreach (RestrictionKey key in spent)
        {
            foreach (byte[] hashed in Candidates(key))
            {
                if (await SpentAsync(hashed, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
        }

        await SweepSettledAsync(at, cancellationToken).ConfigureAwait(false);

        context.Sends.Add(new SendRecord
        {
            Reference = reference,
            Counted = [.. ordered.Select(one => one.Hashed)],
            FingerprintVersion = Fingerprint.CurrentVersion(ring),
            SentAt = at,
            SettlesAt = at + settles,
        });
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
            .AnyAsync(send => send.Reference == reference, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ReleaseAsync(byte[] reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        bool tracked = context.Sends.Local.Any(row => row.Reference.SequenceEqual(reference));

        // The row is keyed by the hash of the reference, so an unknown one finds
        // nothing and a settled one was swept (INT-SMS-005). D-166 X3: it is read under
        // its lock, so two reports releasing it at once release it once.
        SendRecord? send = (await context.Sends
                .FromSql($"SELECT * FROM identity.sends WHERE reference = {reference} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        if (send is null)
        {
            return false;
        }

        if (tracked)
        {
            await context.Entry(send).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        // The send names its keys by their hashes alone, and a hash is kept in one
        // table or the other, so each is looked for in both, in the order a send counts
        // them. One of its times is taken out of the row as it then stands.
        foreach (string table in (string[])[Destinations, Keys])
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

        context.Sends.Remove(send);

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
    // statement over the row as it stands, so sends counted at once are each kept.
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

    private static byte[] Named(RestrictionKey key) =>
        Encoding.UTF8.GetBytes(key.Restriction + Separator + key.Value);

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

    private async ValueTask CountAsync(
        SendCount count,
        byte[] hashed,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Counting(count.Key.Kind is RestrictionKeyKind.Destination ? Destinations : Keys),
                new { key = hashed, version = Fingerprint.CurrentVersion(ring), at, since = at - count.Retain },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    // Credit support granted under one version of the key, spent by one send with the
    // grant's row held; false where no credit stands under that version.
    private async ValueTask<bool> SpentAsync(byte[] hashed, CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        int? credit = await ambient.Connection
            .ExecuteScalarAsync<int?>(new CommandDefinition(
                Credit,
                new { key = hashed },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (credit is not int standing)
        {
            return false;
        }

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                standing > 1 ? Spend : SpendLast,
                new { key = hashed },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return true;
    }

    private byte[] Hashed(RestrictionKey key) => Fingerprint.Compute(Named(key), ring);

    private IReadOnlyList<byte[]> Candidates(RestrictionKey key) =>
        Fingerprint.Candidates(Named(key), ring);
}
