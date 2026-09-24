using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Callbacks;

/// <summary>
/// Where inbound callbacks are counted per source.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="fingerprintKey">What the sources are hashed under.</param>
/// <remarks>Implements INT-GEN-003, BFF-MACH-003 and CONV-DESIGN-003.</remarks>
internal sealed class CallbackLedger(StoreContext context, ReadOnlyMemory<byte> fingerprintKey)
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

        byte[] hashed = Hashed(source);

        // A fixed window, so the count restarts at the interval boundary.
        DateTimeOffset opened = new(at.UtcTicks - (at.UtcTicks % window.Ticks), TimeSpan.Zero);

        context.Callbacks.Add(new CallbackRecord
        {
            Id = Guid.CreateVersion7(at),
            Source = hashed,
            At = at,
            Rejected = false,
        });

        int made = await context.Callbacks
            .CountAsync(
                callback => callback.Source == hashed && callback.At >= opened,
                cancellationToken)
            .ConfigureAwait(false);

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

        byte[] hashed = Hashed(source);

        context.Callbacks.Add(new CallbackRecord
        {
            Id = Guid.CreateVersion7(at),
            Source = hashed,
            At = at,
            Rejected = true,
        });

        int rejected = await context.Callbacks
            .CountAsync(
                callback => callback.Source == hashed && callback.Rejected && callback.At >= from,
                cancellationToken)
            .ConfigureAwait(false);

        return rejected + 1;
    }

    private byte[] Hashed(string source) =>
        Fingerprint.Compute(Encoding.UTF8.GetBytes(source), fingerprintKey.Span);
}
