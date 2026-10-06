using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Callbacks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Callbacks;

/// <summary>
/// Where inbound callbacks are counted per source.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="connections">Where the lock statement takes its connection from.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <remarks>
/// Implements INT-GEN-003, BFF-MACH-003, OPS-SEC-003 and CONV-DESIGN-003. A callback
/// recorded under a previous version of the fingerprint key still counts until the
/// rotation retires it.
/// </remarks>
internal sealed class CallbackLedger(StoreContext context, DataConnections connections, IKeyRing ring)
    : ICallbackLedger
{
    // D-166 X3: a callback is admitted on the count of those committed before it, so a
    // burst would each count without the others. The source's callbacks are held for
    // the rest of the transaction; no read takes this lock.
    private const string Hold =
        """
        SELECT pg_advisory_xact_lock(hashtextextended(
            'identity.callbacks/' || encode(@source, 'hex'),
            0));
        """;

    private static readonly TimeSpan Kept = TimeSpan.FromHours(1);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask HoldAsync(string source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A source's callbacks are held only inside the operation's transaction.");
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Hold,
                new { source = Candidates(source)[0] },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<int> ReceivedAsync(
        string source,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        await SweepAsync(at, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<byte[]> candidates = Candidates(source);

        // A fixed window, so the count restarts at the interval boundary.
        DateTimeOffset opened = new(at.UtcTicks - (at.UtcTicks % window.Ticks), TimeSpan.Zero);

        context.Callbacks.Add(new CallbackRecord
        {
            Id = Guid.CreateVersion7(at),
            Source = candidates[0],
            FingerprintVersion = Fingerprint.CurrentVersion(ring),
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
            FingerprintVersion = Fingerprint.CurrentVersion(ring),
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

    /// <inheritdoc/>
    public async ValueTask SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        DateTimeOffset oldest = now - Kept;

        _ = await context.Callbacks
            .Where(callback => callback.At < oldest)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // The current version first, which is the one a callback is recorded under.
    private IReadOnlyList<byte[]> Candidates(string source) =>
        Fingerprint.Candidates(Encoding.UTF8.GetBytes(source), ring);
}
