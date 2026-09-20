using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// Where the addresses already told there is no account are remembered.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <param name="fingerprintKey">What the addresses are hashed under.</param>
/// <remarks>Implements AUTH-ABUSE-003 and CONV-DESIGN-003.</remarks>
internal sealed class NoticeLedger(JanusDbContext context, ReadOnlyMemory<byte> fingerprintKey)
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

        byte[] hashed = Hashed(destination);
        DateTimeOffset opened = at - window;

        bool told = await context.NonexistenceNotices
            .AnyAsync(
                notice => notice.Destination == hashed && notice.At > opened,
                cancellationToken)
            .ConfigureAwait(false);

        if (told)
        {
            return false;
        }

        DateTimeOffset oldest = at - (window > Hour ? window : Hour);

        await context.NonexistenceNotices
            .Where(notice => notice.At < oldest)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        context.NonexistenceNotices.Add(new NoticeRecord
        {
            Id = Guid.CreateVersion7(at),
            Destination = hashed,
            At = at,
        });

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<int> SinceAsync(DateTimeOffset from, CancellationToken cancellationToken) =>
        await context.NonexistenceNotices
            .CountAsync(notice => notice.At >= from, cancellationToken)
            .ConfigureAwait(false);

    private byte[] Hashed(string destination) =>
        Fingerprint.Compute(Encoding.UTF8.GetBytes(destination), fingerprintKey.Span);
}
