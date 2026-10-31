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
/// after the other and never wait on each other in a ring (D-183). The rows of the
/// messages of a send are taken before any counter, in the order of their references,
/// wherever more than the counters is held: a retry, a release, a report, an answer.
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

    // A message judged again keeps the send it is of and what its send is answered by.
    private const string Admitted =
        """
        INSERT INTO identity.sends AS held
            (reference, send, admitted, counted, spent, spent_versions, fingerprint_version,
             sent_at, settles_at, resend, resend_at, second_factor, answer)
        VALUES (@reference, @send, TRUE, @counted, @spent, @versions, @version,
             @at, @settles, @resend, @resendAt, @secondFactor, @answer)
        ON CONFLICT (reference) DO UPDATE
        SET admitted = TRUE,
            released = FALSE,
            counted = EXCLUDED.counted,
            spent = EXCLUDED.spent,
            spent_versions = EXCLUDED.spent_versions,
            fingerprint_version = EXCLUDED.fingerprint_version,
            sent_at = EXCLUDED.sent_at,
            settles_at = EXCLUDED.settles_at,
            resend = EXCLUDED.resend,
            resend_at = EXCLUDED.resend_at,
            second_factor = EXCLUDED.second_factor;
        """;

    // What a send counts once, written the same on the row of every other message of it.
    private const string CountedOnce =
        """
        UPDATE identity.sends
        SET resend = @resend, resend_at = @resendAt, settles_at = GREATEST(settles_at, @settles)
        WHERE send = @send AND NOT (reference = ANY(@references));
        """;

    private const string ReleasedOnce =
        """
        UPDATE identity.sends
        SET resend = CAST(ARRAY[] AS bytea[]), resend_at = NULL
        WHERE send = ANY(@sends);
        """;

    private const string SetAside =
        """
        UPDATE identity.sends
        SET admitted = FALSE,
            counted = CAST(ARRAY[] AS bytea[]),
            spent = CAST(ARRAY[] AS bytea[]),
            spent_versions = CAST(ARRAY[] AS integer[])
        WHERE reference = @reference;
        """;

    private const string Bound =
        """
        UPDATE identity.sends SET answer = @answer WHERE send = @send;
        """;

    // INT-SMS-005 (D-227): a message failed for good is still one the library handed
    // over, so its row stays until its send settles, holding no count.
    private const string MarkedReleased =
        """
        UPDATE identity.sends
        SET released = TRUE,
            admitted = FALSE,
            counted = CAST(ARRAY[] AS bytea[]),
            spent = CAST(ARRAY[] AS bytea[]),
            spent_versions = CAST(ARRAY[] AS integer[])
        WHERE reference = @reference;
        """;

    // A send is forgotten whole: a message of it stands while any other of the send
    // could still be reported on, so the last of them to fail is still known as the last.
    private const string Settled =
        """
        DELETE FROM identity.sends AS settled
        WHERE settled.settles_at < @now
          AND NOT EXISTS (
              SELECT 1 FROM identity.sends AS other
              WHERE other.send = settled.send AND other.settles_at >= @now);
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

        // A message judged again is read under the lock of every row of its send first,
        // as a delivery report's release reads it, so the two release it once between
        // them.
        SendRecord? retried = setAside is null
            ? null
            : Named(await SendOfAsync(setAside, cancellationToken).ConfigureAwait(false), setAside);

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
            await SetAsideAsync(ambient, retried, cancellationToken).ConfigureAwait(false);
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

                // What the send of a message judged again counted once is left out of
                // what it is judged on, and stands until the message is admitted or
                // fails for good.
                if (retried is { ResendAt: DateTimeOffset once }
                    && retried.Resend.Any(counted => CryptographicOperations.FixedTimeEquals(counted, hashed)))
                {
                    _ = sent.Remove(once);
                }
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
    /// <exception cref="ArgumentNullException">The messages or what the send counts once are absent.</exception>
    /// <exception cref="ArgumentException">No message is given.</exception>
    public async ValueTask RecordAsync(
        IReadOnlyList<CountedMessage> messages,
        SendPlan once,
        bool secondFactor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(once);

        if (messages.Count == 0)
        {
            throw new ArgumentException("A send counted is at least one message.", nameof(messages));
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        // A message judged again stays of the send it was first counted under, whose
        // rows its judgement took; at a first admission the first message names the
        // send, and no row of it stands yet.
        List<SendRecord> standing = await SendOfAsync(messages[0].Reference, cancellationToken)
            .ConfigureAwait(false);

        byte[] send = standing.Count > 0 ? standing[0].Send : messages[0].Reference;

        // AUTH-ABUSE-004: a send judged again counts at this instant in place of its
        // earlier count, against the restrictions marked resend as against the others.
        await TakenOutAsync(ambient, CountedOnceBy(standing), cancellationToken).ConfigureAwait(false);

        (SendCount Count, byte[] Hashed)[] counted = InOrder(once.Counted);
        TimeSpan kept = TimeSpan.Zero;

        foreach ((SendCount count, byte[] hashed) in counted)
        {
            await CountAsync(count, hashed, at, cancellationToken).ConfigureAwait(false);

            if (count.Retain > kept)
            {
                kept = count.Retain;
            }
        }

        // Credit spent against a restriction marked resend is spent by the send, and no
        // message of it holds it to give back.
        _ = await CreditedAsync(once.Spent, cancellationToken).ConfigureAwait(false);

        byte[][] resend = [.. counted.Select(one => one.Hashed)];
        DateTimeOffset? resendAt = resend.Length > 0 ? at : null;

        await SweepSettledAsync(at, cancellationToken).ConfigureAwait(false);

        foreach (CountedMessage message in messages)
        {
            TimeSpan settles = kept;

            // The rows are written in the order they were taken in, the destinations'
            // table first and each table's keys by their hash.
            (SendCount Count, byte[] Hashed)[] ordered = InOrder(message.Plan.Counted);

            foreach ((SendCount count, byte[] hashed) in ordered)
            {
                await CountAsync(count, hashed, at, cancellationToken).ConfigureAwait(false);

                if (count.Retain > settles)
                {
                    settles = count.Retain;
                }
            }

            List<(byte[] Hashed, int Version)> credited = await CreditedAsync(message.Plan.Spent, cancellationToken)
                .ConfigureAwait(false);

            // The row is written by its own statement and not tracked for the commit, so
            // a release later in the same transaction finds it.
            _ = await ambient.Connection
                .ExecuteAsync(new CommandDefinition(
                    Admitted,
                    new
                    {
                        reference = message.Reference,
                        send,
                        counted = ordered.Select(one => one.Hashed).ToArray(),
                        spent = credited.Select(one => one.Hashed).ToArray(),
                        versions = credited.Select(one => one.Version).ToArray(),
                        version = Fingerprint.CurrentVersion(ring),
                        at,
                        settles = at + settles,
                        resend,
                        resendAt,
                        secondFactor,
                        answer = standing.Select(one => one.Answer).FirstOrDefault(one => one is not null),
                    },
                    ambient.Transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                CountedOnce,
                new
                {
                    send,
                    references = messages.Select(message => message.Reference).ToArray(),
                    resend,
                    resendAt,
                    settles = at + kept,
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SetAsideAsync(byte[] reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        if (Named(await SendOfAsync(reference, cancellationToken).ConfigureAwait(false), reference) is SendRecord message)
        {
            await SetAsideAsync(ambient, message, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask SweepSettledAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Settled,
                new { now },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HoldsAsync(byte[] reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return await context.Sends
            .AsNoTracking()
            .AnyAsync(send => send.Reference == reference && send.Admitted, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ReleaseAsync(byte[] reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        // The row is keyed by the hash of the reference, so an unknown one finds
        // nothing and a settled one was swept (INT-SMS-005). D-166 X3: it is read under
        // the lock of every row of its send, so two releases of it at once release it
        // once, and two messages of one send failing at once fail one after the other.
        List<SendRecord> messages = await SendOfAsync(reference, cancellationToken).ConfigureAwait(false);

        if (Named(messages, reference) is not SendRecord message)
        {
            return false;
        }

        // INT-SMS-005 (D-227): a message already released is released once.
        if (message.Released)
        {
            return true;
        }

        // AUTH-ABUSE-004, INT-SMS-005: the last message of a send to fail for good takes
        // what the send counted once against the restrictions marked resend with it.
        bool last = messages.Count(one => !one.Released) == 1;
        (byte[] Hashed, DateTimeOffset Sent)[] once = last ? CountedOnceBy(messages) : [];

        await TakenOutAsync(ambient, [.. CountedBy(message), .. once], cancellationToken).ConfigureAwait(false);

        await GivenBackAsync(ambient, message, cancellationToken).ConfigureAwait(false);

        if (last)
        {
            _ = await ambient.Connection
                .ExecuteAsync(new CommandDefinition(
                    ReleasedOnce,
                    new { sends = new[] { message.Send } },
                    ambient.Transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                MarkedReleased,
                new { reference = message.Reference },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeliveredAsync(byte[] reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        List<SendRecord> messages = await SendOfAsync(reference, cancellationToken).ConfigureAwait(false);

        if (Named(messages, reference) is not SendRecord message)
        {
            return false;
        }

        // INT-SMS-005: a report of delivery releases what a send of purpose secondfactor
        // counted once, and nothing else of any send; one of a message already released
        // changes nothing (D-227).
        if (message.SecondFactor && !message.Released)
        {
            await ReleasedOnceAsync(ambient, messages, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask BindAsync(byte[] reference, byte[] answer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(answer);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Bound,
                new { send = reference, answer },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AnsweredAsync(byte[] answer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(answer);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        List<SendRecord> messages = await context.Sends
            .FromSql($"SELECT * FROM identity.sends WHERE answer = {answer} ORDER BY reference FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await ReleasedOnceAsync(ambient, messages, cancellationToken).ConfigureAwait(false);
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

    private static SendRecord? Named(List<SendRecord> messages, byte[] reference) =>
        messages.SingleOrDefault(message => CryptographicOperations.FixedTimeEquals(message.Reference, reference));

    // The times one message counted, by the hashed key each stands against.
    private static IEnumerable<(byte[] Hashed, DateTimeOffset Sent)> CountedBy(SendRecord message) =>
        message.Counted.Select(hashed => (hashed, message.SentAt));

    // The time each send among the messages counted once, by the hashed key it stands
    // against: every row of a send carries it, so one row of each answers.
    private static (byte[] Hashed, DateTimeOffset Sent)[] CountedOnceBy(List<SendRecord> messages) =>
    [
        .. messages
            .Where(message => message.ResendAt is not null)
            .GroupBy(message => Convert.ToHexString(message.Send), StringComparer.Ordinal)
            .Select(send => send.First())
            .SelectMany(message => message.Resend.Select(hashed => (hashed, message.ResendAt!.Value))),
    ];

    // The rows a judgement takes, in the one order every judgement takes them in. A key
    // of the send is taken in its own table under every version of the fingerprint key
    // held; a hash a message being judged again counted against, or its send counted
    // once against, is kept in one table or the other, so it is taken in both.
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

        foreach (byte[] hashed in (byte[][])[.. retried?.Counted ?? [], .. retried?.Resend ?? []])
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

    // One message taken back out of every bucket it counted against and the credit it
    // spent given back, while its row stays, holding the count of no admission: it is
    // still of its send, which a failure for good of it is told by.
    private async ValueTask SetAsideAsync(
        AmbientConnection ambient,
        SendRecord message,
        CancellationToken cancellationToken)
    {
        await TakenOutAsync(ambient, [.. CountedBy(message)], cancellationToken).ConfigureAwait(false);
        await GivenBackAsync(ambient, message, cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                SetAside,
                new { reference = message.Reference },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    // What each send among the messages counted once against the restrictions marked
    // resend taken back, and no row of it holding it any longer.
    private static async ValueTask ReleasedOnceAsync(
        AmbientConnection ambient,
        List<SendRecord> messages,
        CancellationToken cancellationToken)
    {
        (byte[] Hashed, DateTimeOffset Sent)[] counted = CountedOnceBy(messages);

        if (counted.Length == 0)
        {
            return;
        }

        await TakenOutAsync(ambient, counted, cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                ReleasedOnce,
                new { sends = messages.Select(message => message.Send).ToArray() },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    // Times taken back out of the counters they stand in. A count names its key by the
    // hash alone, and a hash is kept in one table or the other, so each is looked for in
    // both, in the order a judgement takes the counters. One time is taken out of the
    // row as it then stands.
    private static async ValueTask TakenOutAsync(
        AmbientConnection ambient,
        (byte[] Hashed, DateTimeOffset Sent)[] counted,
        CancellationToken cancellationToken)
    {
        foreach (string table in Tables)
        {
            foreach ((byte[] hashed, DateTimeOffset sent) in counted
                .OrderBy(one => Convert.ToHexString(one.Hashed), StringComparer.Ordinal))
            {
                _ = await ambient.Connection
                    .ExecuteAsync(new CommandDefinition(
                        Released(table),
                        new { key = hashed, sent },
                        ambient.Transaction,
                        cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }
        }
    }

    // The credit a message spent given back where the version it was granted under is
    // still held.
    private async ValueTask GivenBackAsync(AmbientConnection ambient, SendRecord send, CancellationToken cancellationToken)
    {
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
    }

    // The keys in the order they were taken in, the destinations' table first and each
    // table's keys by their hash.
    private (SendCount Count, byte[] Hashed)[] InOrder(IReadOnlyCollection<SendCount> counted) =>
    [
        .. counted
            .Select(count => (Count: count, Hashed: Hashed(count.Key)))
            .OrderBy(one => one.Count.Key.Kind is RestrictionKeyKind.Destination ? 0 : 1)
            .ThenBy(one => Convert.ToHexString(one.Hashed), StringComparer.Ordinal),
    ];

    // The credit spent under each key, by the hash and the version of the grant it was
    // spent from.
    private async ValueTask<List<(byte[] Hashed, int Version)>> CreditedAsync(
        IReadOnlyCollection<RestrictionKey> spent,
        CancellationToken cancellationToken)
    {
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

        return credited;
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

    // The rows of every message of the send one message is of, taken in the order of
    // their references and held. A row is keyed by the hash of the reference, so an
    // unknown one finds nothing and a settled one was swept (INT-SMS-005). D-166 X3:
    // they are read under their locks, so two releases at once release once.
    private async ValueTask<List<SendRecord>> SendOfAsync(byte[] reference, CancellationToken cancellationToken) =>
        await context.Sends
            .FromSql(
                $"""
                SELECT * FROM identity.sends
                WHERE send = (SELECT send FROM identity.sends WHERE reference = {reference})
                ORDER BY reference
                FOR UPDATE
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private bool Holds(int version) =>
        ring.BorrowFingerprintKeys(keys => keys.Versions.ContainsKey(version))
            .Match(held => held, error => throw new InvalidOperationException(error.Code.ToString()));

    private byte[] Hashed(RestrictionKey key) => Fingerprint.Compute(Named(key), ring);

    private IReadOnlyList<byte[]> Candidates(RestrictionKey key) =>
        Fingerprint.Candidates(Named(key), ring);
}
