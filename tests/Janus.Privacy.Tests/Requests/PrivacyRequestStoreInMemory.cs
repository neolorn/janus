using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Requests;

namespace Janus.Privacy.Tests.Requests;

/// <summary>
/// The request queue, in a list, so a test can read back what was written.
/// </summary>
internal sealed class PrivacyRequestStoreInMemory : IPrivacyRequestStore
{
    private readonly List<QueuedRequest> _queue = [];

    /// <summary>
    /// The queue as it stands.
    /// </summary>
    public IReadOnlyList<QueuedRequest> Queue => _queue;

    /// <inheritdoc/>
    public ValueTask AddAsync(QueuedRequest request, CancellationToken cancellationToken)
    {
        _queue.Add(request);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<QueuedRequest?> FindAsync(
        PrivacyRequestId request,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_queue.Find(held => held.Id == request));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<QueuedRequest>> AllAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<QueuedRequest>>(
            [.. _queue.OrderBy(request => request.CreatedAt)]);

    /// <inheritdoc/>
    public ValueTask<bool> OpenAsync(
        SubjectId subject,
        PrivacyRequestType type,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_queue.Exists(request =>
            request.Subject == subject && request.Type == type && request.Open));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<QueuedRequest>> ReachedAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<QueuedRequest>>(
        [
            .. _queue
                .Where(request => request.Open && request.WarnAt <= now)
                .OrderBy(request => request.DecisionDue),
        ]);

    /// <inheritdoc/>
    public ValueTask RecordAsync(QueuedRequest request, CancellationToken cancellationToken)
    {
        if (!_queue.Contains(request))
        {
            throw new InvalidOperationException("The request has no row to record a change on.");
        }

        return ValueTask.CompletedTask;
    }
}
