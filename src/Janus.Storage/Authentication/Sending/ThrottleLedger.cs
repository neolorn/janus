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
/// Where failed attempts are counted, under the hash of what they were made against.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <remarks>
/// Implements AUTH-ABUSE-001, OPS-SEC-003 and CONV-DESIGN-003. A counter kept under a
/// previous version of the fingerprint key is read until the rotation retires the
/// version, and the next failure is counted under the current one from where it stood.
/// </remarks>
internal sealed class ThrottleLedger(StoreContext context, IKeyRing ring)
    : IThrottleLedger
{
    /// <inheritdoc/>
    public async ValueTask<ThrottleCounter?> FindAsync(
        ThrottleScope scope,
        string key,
        CancellationToken cancellationToken)
    {
        foreach (byte[] hashed in Candidates(key))
        {
            ThrottleRecord? counter = await context.ThrottleCounters
                .FindAsync([scope, hashed], cancellationToken)
                .ConfigureAwait(false);

            if (counter is not null)
            {
                return new ThrottleCounter(counter.Failures, counter.At);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask FailedAsync(
        ThrottleScope scope,
        string key,
        int standing,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        byte[] hashed = Hashed(key);

        ThrottleRecord? counter = await context.ThrottleCounters
            .FindAsync([scope, hashed], cancellationToken)
            .ConfigureAwait(false);

        if (counter is null)
        {
            context.ThrottleCounters.Add(new ThrottleRecord
            {
                Scope = scope,
                Key = hashed,
                FingerprintVersion = Fingerprint.CurrentVersion(ring),
                Failures = standing + 1,
                At = at,
            });

            return;
        }

        counter.Failures = standing + 1;
        counter.At = at;
    }

    /// <inheritdoc/>
    public async ValueTask ClearAsync(
        ThrottleScope scope,
        string key,
        CancellationToken cancellationToken)
    {
        foreach (byte[] hashed in Candidates(key))
        {
            ThrottleRecord? counter = await context.ThrottleCounters
                .FindAsync([scope, hashed], cancellationToken)
                .ConfigureAwait(false);

            if (counter is not null)
            {
                context.ThrottleCounters.Remove(counter);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask SweepAsync(DateTimeOffset now, TimeSpan halfLife, CancellationToken cancellationToken)
    {
        double halfLives = halfLife.TotalSeconds;

        // Throttle.Standing rounds the decayed count, so a counter stands at nothing once
        // it has halved below one half: after log2(2 * failures) half-lives. Read as a
        // logarithm, the age never overflows however long the counter has stood.
        _ = await context.ThrottleCounters
            .Where(counter => counter.Failures <= 0
                || (halfLives > 0
                    && (now - counter.At).TotalSeconds > halfLives * Math.Log(2.0 * counter.Failures) / Math.Log(2.0)))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public byte[] Identify(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        return Hashed(identifier);
    }

    private byte[] Hashed(string key) =>
        Fingerprint.Compute(Encoding.UTF8.GetBytes(key), ring);

    private IReadOnlyList<byte[]> Candidates(string key) =>
        Fingerprint.Candidates(Encoding.UTF8.GetBytes(key), ring);
}
