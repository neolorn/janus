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
/// Where the addresses already told there is no account are remembered.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <remarks>
/// Implements AUTH-ABUSE-003, OPS-SEC-003 and CONV-DESIGN-003. A notice recorded under
/// a previous version of the fingerprint key still counts until the rotation retires it.
/// </remarks>
internal sealed class NoticeLedger(StoreContext context, IKeyRing ring)
    : INoticeLedger
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    /// <inheritdoc/>
    public async ValueTask<bool> FirstAsync(
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
                return false;
            }
        }

        await SweepAsync(at, window, cancellationToken).ConfigureAwait(false);

        context.NonexistenceNotices.Add(new NoticeRecord
        {
            Id = Guid.CreateVersion7(at),
            Destination = Fingerprint.Compute(Encoding.UTF8.GetBytes(destination), ring),
            FingerprintVersion = Fingerprint.CurrentVersion(ring),
            At = at,
        });

        return true;
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
