using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Requests;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Requests;

/// <summary>
/// The data subject request queue, over the <c>privacy_requests</c> table.
/// </summary>
/// <param name="context">The context the operation's reads and writes run on.</param>
/// <remarks>Implements PRIV-RIGHT-001, PRIV-RIGHT-002 and CONV-DESIGN-003.</remarks>
internal sealed class PrivacyRequestStore(JanusDbContext context) : IPrivacyRequestStore
{
    /// <inheritdoc/>
    public async ValueTask AddAsync(QueuedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await context.PrivacyRequests
            .AddAsync(
                new PrivacyRequestRecord
                {
                    Id = request.Id,
                    Subject = request.Subject,
                    Type = request.Type,
                    Detail = request.Detail,
                    ReceivedAt = request.ReceivedAt,
                    CreatedAt = request.CreatedAt,
                    DecisionDue = request.DecisionDue,
                    WarnAt = request.WarnAt,
                    EscalateAt = request.EscalateAt,
                    Status = request.Status,
                    Channel = request.Channel,
                    IdentityConfirmation = request.IdentityConfirmation,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<QueuedRequest?> FindAsync(
        PrivacyRequestId request,
        CancellationToken cancellationToken)
    {
        PrivacyRequestRecord? held = await context.PrivacyRequests
            .SingleOrDefaultAsync(row => row.Id == request, cancellationToken)
            .ConfigureAwait(false);

        return held is null ? null : Read(held);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<QueuedRequest>> AllAsync(
        CancellationToken cancellationToken) =>
    [
        .. (await context.PrivacyRequests
                .OrderBy(request => request.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Read),
    ];

    /// <inheritdoc/>
    public async ValueTask<bool> OpenAsync(
        SubjectId subject,
        PrivacyRequestType type,
        CancellationToken cancellationToken) =>
        await context.PrivacyRequests
            .AnyAsync(
                request => request.Subject == subject
                    && request.Type == type
                    && request.Status == PrivacyRequestStatus.Open,
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<QueuedRequest>> ReachedAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
    [
        .. (await context.PrivacyRequests
                .Where(request => request.Status == PrivacyRequestStatus.Open
                    && request.WarnAt <= now)
                .OrderBy(request => request.DecisionDue)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Read),
    ];

    /// <inheritdoc/>
    public async ValueTask RecordAsync(QueuedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        PrivacyRequestRecord held = await context.PrivacyRequests
            .SingleOrDefaultAsync(row => row.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The request has no row to record a change on.");

        held.Status = request.Status;
        held.DecidedAt = request.DecidedAt;
        held.DecisionReason = request.DecisionReason;
        held.WarnedAt = request.WarnedAt;
        held.EscalatedAt = request.EscalatedAt;
    }

    private static QueuedRequest Read(PrivacyRequestRecord row) =>
        QueuedRequest.Existing(
            new HeldRequest(
                row.Id,
                row.Subject,
                row.Type,
                row.Detail,
                row.ReceivedAt,
                row.CreatedAt,
                row.DecisionDue,
                row.WarnAt,
                row.EscalateAt,
                row.Status,
                row.DecidedAt,
                row.DecisionReason,
                row.Channel,
                row.IdentityConfirmation,
                row.WarnedAt,
                row.EscalatedAt));
}
