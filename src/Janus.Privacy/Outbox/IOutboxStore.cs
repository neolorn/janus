using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Outbox;

/// <summary>
/// Where the outbox rows are read and written.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003a and CONV-DESIGN-003. The row is added inside the
/// transaction that made the fact true, so nothing here commits: the caller's unit of
/// work does, and a rollback leaves neither the fact nor the delivery.
/// </remarks>
internal interface IOutboxStore
{
    /// <summary>
    /// Writes a delivery onto the transaction in progress.
    /// </summary>
    /// <param name="delivery">The delivery.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask AddAsync(Delivery delivery, CancellationToken cancellationToken);

    /// <summary>
    /// Every delivery awaiting subscribers whose next attempt is due.
    /// </summary>
    /// <param name="now">The instant the attempt is due by.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The deliveries, oldest first.</returns>
    ValueTask<IReadOnlyList<Delivery>> DueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads one delivery.
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The delivery, or nothing where no such row exists.</returns>
    ValueTask<Delivery?> FindAsync(DeliveryId delivery, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the progress a delivery has made onto its row, its confirmations with
    /// it.
    /// </summary>
    /// <param name="delivery">The delivery as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(Delivery delivery, CancellationToken cancellationToken);

    /// <summary>
    /// One delivery, with when each subscriber confirmed it.
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The delivery and its confirmations, or nothing where no such row exists.</returns>
    ValueTask<DeliveryProgress?> ProgressAsync(
        DeliveryId delivery,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every delivery of one kind whose host-side work is outstanding, awaiting
    /// subscribers or failed, with when each subscriber confirmed it, in one query.
    /// </summary>
    /// <param name="kind">Which fact.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The deliveries and their confirmations, oldest first.</returns>
    ValueTask<IReadOnlyList<DeliveryProgress>> OutstandingAsync(
        SubjectEventKind kind,
        CancellationToken cancellationToken);

    /// <summary>
    /// The latest delivery of one kind about one subject, with when each subscriber
    /// confirmed it.
    /// </summary>
    /// <param name="subject">Whose fact it is.</param>
    /// <param name="kind">Which fact it is.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The delivery and its confirmations, or nothing where none exists.</returns>
    ValueTask<DeliveryProgress?> LatestAsync(
        SubjectId subject,
        SubjectEventKind kind,
        CancellationToken cancellationToken);
}
