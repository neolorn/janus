using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The sending ledger over the three tables that hold what has gone out: the counter
/// per restriction key, the credit granted to one, and the sends a delivery report
/// can still take back.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="fingerprintKey">What the restriction keys are hashed under.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, INT-SMS-005 and CONV-DESIGN-003. The plain key value
/// crosses into this class and no further.
/// </remarks>
internal sealed class SendLedger(StoreContext context, ReadOnlyMemory<byte> fingerprintKey)
    : ISendLedger
{
    private const string Separator = "\u0000";

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<RestrictionKey, SendCounter>> CountersAsync(
        IReadOnlyCollection<RestrictionKey> keys,
        DateTimeOffset stale,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);

        // AUTH-ABUSE-004 AC6: a record whose newest time decides nothing holds
        // nothing, so it goes before anything is read rather than standing until the
        // key it names is sent to again. The times are written oldest first, so the
        // newest is the last of them and the index is over that expression.
        _ = await context.SendCounters
            .Where(counter => counter.SentAt[counter.SentAt.Length - 1] < stale)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var standing = new Dictionary<RestrictionKey, SendCounter>();

        foreach (RestrictionKey key in keys)
        {
            byte[] hashed = Hashed(key);

            SendCounterRecord? counter = await context.SendCounters
                .FindAsync([hashed], cancellationToken)
                .ConfigureAwait(false);

            SendGrantRecord? grant = await context.SendGrants
                .FindAsync([hashed], cancellationToken)
                .ConfigureAwait(false);

            if (counter is null && grant is null)
            {
                continue;
            }

            standing[key] = new SendCounter(counter?.SentAt ?? [], grant?.Credit ?? 0);
        }

        return standing;
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
            byte[] hashed = Hashed(count.Key);

            SendCounterRecord? counter = await context.SendCounters
                .FindAsync([hashed], cancellationToken)
                .ConfigureAwait(false);

            DateTimeOffset[] kept =
            [
                .. (counter?.SentAt ?? []).Where(sent => sent > at - count.Retain),
                at,
            ];

            if (counter is null)
            {
                context.SendCounters.Add(new SendCounterRecord { Key = hashed, SentAt = kept });
            }
            else
            {
                counter.SentAt = kept;
            }

            if (count.Retain > settles)
            {
                settles = count.Retain;
            }
        }

        foreach (RestrictionKey key in spent)
        {
            SendGrantRecord? grant = await context.SendGrants
                .FindAsync([Hashed(key)], cancellationToken)
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
        }

        await context.Sends
            .Where(send => send.SettlesAt < at)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        context.Sends.Add(new SendRecord
        {
            Reference = reference,
            Counted = [.. counted.Select(count => Hashed(count.Key))],
            SentAt = at,
            SettlesAt = at + settles,
        });
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

        foreach (byte[] hashed in send.Counted)
        {
            SendCounterRecord? counter = await context.SendCounters
                .FindAsync([hashed], cancellationToken)
                .ConfigureAwait(false);

            if (counter is null)
            {
                continue;
            }

            DateTimeOffset[] kept = Without(counter.SentAt, send.SentAt);

            if (kept.Length == 0)
            {
                context.SendCounters.Remove(counter);
            }
            else
            {
                counter.SentAt = kept;
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
            context.SendGrants.Add(new SendGrantRecord { Key = hashed, Credit = credit });

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

    private byte[] Hashed(RestrictionKey key) =>
        Fingerprint.Compute(
            Encoding.UTF8.GetBytes(key.Restriction + Separator + key.Value),
            fingerprintKey.Span);
}
