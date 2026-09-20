using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Requests;

/// <summary>
/// Where data subject requests are read and written.
/// </summary>
/// <remarks>Implements PRIV-RIGHT-001, PRIV-RIGHT-002 and CONV-DESIGN-003.</remarks>
internal interface IPrivacyRequestStore
{
    /// <summary>
    /// Puts a request on the queue.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of adding it.</returns>
    ValueTask AddAsync(QueuedRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one request.
    /// </summary>
    /// <param name="request">Which one.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The request, or nothing where no such row exists.</returns>
    ValueTask<QueuedRequest?> FindAsync(
        PrivacyRequestId request,
        CancellationToken cancellationToken);

    /// <summary>
    /// The whole queue, decided requests included: the record persists.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The requests, oldest first.</returns>
    ValueTask<IReadOnlyList<QueuedRequest>> AllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether the subject already has an undecided request of one type, which is
    /// what makes a second one a duplicate.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="type">Which type.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether one is open.</returns>
    ValueTask<bool> OpenAsync(
        SubjectId subject,
        PrivacyRequestType type,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every open request the sweep has reached: warning due, escalation due, or
    /// deadline passed.
    /// </summary>
    /// <param name="now">The instant the sweep is running at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The requests, oldest deadline first.</returns>
    ValueTask<IReadOnlyList<QueuedRequest>> ReachedAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Carries what has changed onto the request's row.
    /// </summary>
    /// <param name="request">The request as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(QueuedRequest request, CancellationToken cancellationToken);
}
