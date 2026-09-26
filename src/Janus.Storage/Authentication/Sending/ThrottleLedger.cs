using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// Where failed attempts are counted, under the hash of what they were made against.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="fingerprintKeys">The versions the scope keys are hashed under.</param>
/// <remarks>
/// Implements AUTH-ABUSE-001, OPS-SEC-003 and CONV-DESIGN-003. A counter kept under a
/// previous version of the fingerprint key is read until the rotation retires the
/// version, and the next failure is counted under the current one from where it stood.
/// </remarks>
internal sealed class ThrottleLedger(StoreContext context, FingerprintKeys fingerprintKeys)
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
                FingerprintVersion = fingerprintKeys.CurrentVersion,
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

    private byte[] Hashed(string key) =>
        Fingerprint.Compute(Encoding.UTF8.GetBytes(key), fingerprintKeys);

    private IReadOnlyList<byte[]> Candidates(string key) =>
        Fingerprint.Candidates(Encoding.UTF8.GetBytes(key), fingerprintKeys);
}
