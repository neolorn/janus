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
/// Where the addresses already told there is no account are remembered.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="connections">Where the lock statement takes its connection from.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <remarks>
/// Implements AUTH-ABUSE-003, OPS-SEC-003 and CONV-DESIGN-003. A notice recorded under
/// a previous version of the fingerprint key still counts until the rotation retires it.
/// </remarks>
internal sealed class NoticeLedger(StoreContext context, DataConnections connections, IKeyRing ring)
    : INoticeLedger
{
    // D-166 X3: an address is told only where no notice to it stands in the window,
    // which is read before the notice is written, so two asks at once would each find
    // none. The address's notices are held for the rest of the transaction; no read
    // takes this lock.
    private const string Hold =
        """
        SELECT pg_advisory_xact_lock(hashtextextended(
            'identity.nonexistence_notices/' || encode(@destination, 'hex'),
            0));
        """;

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask HoldAsync(string destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An address's notices are held only inside the operation's transaction.");
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Hold,
                new { destination = Fingerprint.Compute(Encoding.UTF8.GetBytes(destination), ring) },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WasToldAsync(
        string destination,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        DateTimeOffset opened = at - window;

        foreach (byte[] hashed in Candidates(destination))
        {
            if (await context.NonexistenceNotices
                    .AnyAsync(
                        notice => notice.Destination == hashed && notice.At > opened,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async ValueTask MarkAsync(
        string destination,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        await SweepAsync(at, window, cancellationToken).ConfigureAwait(false);

        context.NonexistenceNotices.Add(new NoticeRecord
        {
            Id = Guid.CreateVersion7(at),
            Destination = Fingerprint.Compute(Encoding.UTF8.GetBytes(destination), ring),
            FingerprintVersion = Fingerprint.CurrentVersion(ring),
            At = at,
        });
    }

    /// <inheritdoc/>
    public async ValueTask<int> SinceAsync(DateTimeOffset from, CancellationToken cancellationToken) =>
        await context.NonexistenceNotices
            .CountAsync(notice => notice.At >= from, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask SweepAsync(DateTimeOffset now, TimeSpan window, CancellationToken cancellationToken)
    {
        DateTimeOffset oldest = now - (window > Hour ? window : Hour);

        _ = await context.NonexistenceNotices
            .Where(notice => notice.At < oldest)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private IReadOnlyList<byte[]> Candidates(string destination) =>
        Fingerprint.Candidates(Encoding.UTF8.GetBytes(destination), ring);
}
