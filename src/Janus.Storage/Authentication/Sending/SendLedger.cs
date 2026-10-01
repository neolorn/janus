using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, INT-SMS-005, PRIV-RET-005, OPS-SEC-003 and CONV-DESIGN-003.
/// A destination is counted apart from every other key, so each table is swept by the
/// longest interval of its own restrictions (D-166, 122). The plain key value crosses
/// into this class and no further. What was counted or granted under a previous version
/// of the fingerprint key still stands until the rotation retires the version; what is
/// counted or granted now is under the current one.
/// </remarks>
internal sealed class SendLedger(StoreContext context, IKeyRing ring)
    : ISendLedger
{
    private const string Separator = "\u0000";

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

                SendGrantRecord? grant = await context.SendGrants
                    .FindAsync([hashed], cancellationToken)
                    .ConfigureAwait(false);

                found |= times is not null || grant is not null;
                sent.AddRange(times ?? []);
                credit += grant?.Credit ?? 0;
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

        foreach (SendCount count in counted)
        {
            await CountAsync(count, at, cancellationToken).ConfigureAwait(false);

            if (count.Retain > settles)
            {
                settles = count.Retain;
            }
        }

        foreach (RestrictionKey key in spent)
        {
            foreach (byte[] hashed in Candidates(key))
            {
                SendGrantRecord? grant = await context.SendGrants
                    .FindAsync([hashed], cancellationToken)
                    .ConfigureAwait(false);

                if (grant is null)
                {
                    continue;
                }

                grant.Credit--;

                if (grant.Credit <= 0)
                {
                    context.SendGrants.Remove(grant);
                }

                break;
            }
        }

        await SweepSettledAsync(at, cancellationToken).ConfigureAwait(false);

        context.Sends.Add(new SendRecord
        {
            Reference = reference,
            Counted = [.. counted.Select(count => Hashed(count.Key))],
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

        // The row is keyed by the hash of the reference, so an unknown one finds
        // nothing and a settled one was swept (INT-SMS-005).
        SendRecord? send = await context.Sends
            .FindAsync([reference], cancellationToken)
            .ConfigureAwait(false);

        if (send is null)
        {
            return false;
        }

        // The send names its keys by their hashes alone, and a hash is kept in one
        // table or the other, so each is looked for in both.
        foreach (byte[] hashed in send.Counted)
        {
            if (await context.SendCounters.FindAsync([hashed], cancellationToken).ConfigureAwait(false)
                is SendCounterRecord destination)
            {
                DateTimeOffset[] kept = Without(destination.SentAt, send.SentAt);

                if (kept.Length == 0)
                {
                    context.SendCounters.Remove(destination);
                }
                else
                {
                    destination.SentAt = kept;
                }
            }
            else if (await context.SendKeyCounters.FindAsync([hashed], cancellationToken).ConfigureAwait(false)
                is SendKeyCounterRecord key)
            {
                DateTimeOffset[] kept = Without(key.SentAt, send.SentAt);

                if (kept.Length == 0)
                {
                    context.SendKeyCounters.Remove(key);
                }
                else
                {
                    key.SentAt = kept;
                }
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
        byte[] hashed = Hashed(key);

        SendGrantRecord? grant = await context.SendGrants
            .FindAsync([hashed], cancellationToken)
            .ConfigureAwait(false);

        if (grant is null)
        {
            context.SendGrants.Add(new SendGrantRecord
            {
                Key = hashed,
                FingerprintVersion = Fingerprint.CurrentVersion(ring),
                Credit = credit,
            });

            return;
        }

        grant.Credit += credit;
    }

    private static DateTimeOffset[] Without(DateTimeOffset[] sends, DateTimeOffset one)
    {
        var kept = new List<DateTimeOffset>(sends.Length);
        bool dropped = false;

        foreach (DateTimeOffset sent in sends)
        {
            if (!dropped && sent == one)
            {
                dropped = true;

                continue;
            }

            kept.Add(sent);
        }

        return [.. kept];
    }

    // A key's times keep what its restriction still counts, and the send.
    private static DateTimeOffset[] Kept(DateTimeOffset[]? sends, TimeSpan retain, DateTimeOffset at) =>
    [
        .. (sends ?? []).Where(sent => sent > at - retain),
        at,
    ];

    private static byte[] Named(RestrictionKey key) =>
        Encoding.UTF8.GetBytes(key.Restriction + Separator + key.Value);

    // A destination is kept in its own table and every other key in the other.
    private async ValueTask<DateTimeOffset[]?> TimesAsync(
        RestrictionKeyKind kind,
        byte[] hashed,
        CancellationToken cancellationToken) =>
        kind is RestrictionKeyKind.Destination
            ? (await context.SendCounters.FindAsync([hashed], cancellationToken).ConfigureAwait(false))?.SentAt
            : (await context.SendKeyCounters.FindAsync([hashed], cancellationToken).ConfigureAwait(false))?.SentAt;

    private async ValueTask CountAsync(SendCount count, DateTimeOffset at, CancellationToken cancellationToken)
    {
        byte[] hashed = Hashed(count.Key);

        if (count.Key.Kind is RestrictionKeyKind.Destination)
        {
            SendCounterRecord? destination = await context.SendCounters
                .FindAsync([hashed], cancellationToken)
                .ConfigureAwait(false);

            if (destination is null)
            {
                context.SendCounters.Add(new SendCounterRecord
                {
                    Key = hashed,
                    FingerprintVersion = Fingerprint.CurrentVersion(ring),
                    SentAt = Kept(null, count.Retain, at),
                });
            }
            else
            {
                destination.SentAt = Kept(destination.SentAt, count.Retain, at);
            }

            return;
        }

        SendKeyCounterRecord? key = await context.SendKeyCounters
            .FindAsync([hashed], cancellationToken)
            .ConfigureAwait(false);

        if (key is null)
        {
            context.SendKeyCounters.Add(new SendKeyCounterRecord
            {
                Key = hashed,
                FingerprintVersion = Fingerprint.CurrentVersion(ring),
                SentAt = Kept(null, count.Retain, at),
            });
        }
        else
        {
            key.SentAt = Kept(key.SentAt, count.Retain, at);
        }
    }

    private byte[] Hashed(RestrictionKey key) => Fingerprint.Compute(Named(key), ring);

    private IReadOnlyList<byte[]> Candidates(RestrictionKey key) =>
        Fingerprint.Candidates(Named(key), ring);
}
