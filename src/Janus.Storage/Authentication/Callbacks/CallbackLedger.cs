using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Callbacks;

/// <summary>
/// Where inbound callbacks are counted per source.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="fingerprintKeys">The versions the sources are hashed under.</param>
/// <remarks>
/// Implements INT-GEN-003, BFF-MACH-003, OPS-SEC-003 and CONV-DESIGN-003. A callback
/// recorded under a previous version of the fingerprint key still counts until the
/// rotation retires it.
/// </remarks>
internal sealed class CallbackLedger(StoreContext context, FingerprintKeys fingerprintKeys)
    : ICallbackLedger
{
    private static readonly TimeSpan Kept = TimeSpan.FromHours(1);

    /// <inheritdoc/>
    public async ValueTask<int> ReceivedAsync(
        string source,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        DateTimeOffset oldest = at - Kept;

        await context.Callbacks
            .Where(callback => callback.At < oldest)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<byte[]> candidates = Candidates(source);

        // A fixed window, so the count restarts at the interval boundary.
        DateTimeOffset opened = new(at.UtcTicks - (at.UtcTicks % window.Ticks), TimeSpan.Zero);

        context.Callbacks.Add(new CallbackRecord
        {
            Id = Guid.CreateVersion7(at),
            Source = candidates[0],
            FingerprintVersion = fingerprintKeys.CurrentVersion,
            At = at,
            Rejected = false,
        });

        int made = 0;

        foreach (byte[] hashed in candidates)
        {
            made += await context.Callbacks
                .CountAsync(
                    callback => callback.Source == hashed && callback.At >= opened,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return made + 1;
    }

    /// <inheritdoc/>
    public async ValueTask<int> RejectedAsync(
        string source,
        DateTimeOffset at,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        IReadOnlyList<byte[]> candidates = Candidates(source);

        context.Callbacks.Add(new CallbackRecord
        {
            Id = Guid.CreateVersion7(at),
            Source = candidates[0],
            FingerprintVersion = fingerprintKeys.CurrentVersion,
            At = at,
            Rejected = true,
        });

        int rejected = 0;

        foreach (byte[] hashed in candidates)
        {
            rejected += await context.Callbacks
                .CountAsync(
                    callback => callback.Source == hashed && callback.Rejected && callback.At >= from,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return rejected + 1;
    }

    // The current version first, which is the one a callback is recorded under.
    private IReadOnlyList<byte[]> Candidates(string source) =>
        Fingerprint.Candidates(Encoding.UTF8.GetBytes(source), fingerprintKeys);
}
