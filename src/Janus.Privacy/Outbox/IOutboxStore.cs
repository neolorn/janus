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
/// work does, and a rollback leaves neither the fact nor the delivery. A pass that carries
/// a row to its subscribers claims it whole first, and writes under that claim alone.
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
    /// Every delivery awaiting subscribers whose next attempt is due and that no pass
    /// holds. The read takes no lock: the claim decides who carries a row.
    /// </summary>
    /// <param name="now">The instant the attempt is due by.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the deliveries are held under, oldest first.</returns>
    ValueTask<IReadOnlyList<DeliveryId>> DueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims one delivery whole for a pass, by one update that succeeds only where the
    /// row awaits subscribers, is due, its next attempt's instant come, and is unclaimed
    /// or its claim has timed out. The caller commits it on its own before any subscriber
    /// is called.
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="now">The instant of the claim.</param>
    /// <param name="timeout">How long the claim stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The claim, or nothing where another pass holds the row, the row is not due or
    /// the row is gone.
    /// </returns>
    ValueTask<DeliveryClaim?> ClaimAsync(
        DeliveryId delivery,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims one completed erasure the off-host ledger has not confirmed, for the pass
    /// that writes its line, by one update that succeeds only where the row is such an
    /// erasure still and is unclaimed or its claim has timed out. A completed row
    /// carries no next attempt. The caller commits it on its own before the ledger is
    /// called.
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="now">The instant of the claim.</param>
    /// <param name="timeout">How long the claim stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The claim, or nothing where another pass holds the row or the row no longer waits
    /// for its line.
    /// </returns>
    ValueTask<DeliveryClaim?> ClaimUnledgeredAsync(
        DeliveryId delivery,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renews a claim before a subscriber is called, by one update conditional on the
    /// claim still being the pass's own, which moves its end to the timeout from this
    /// instant.
    /// </summary>
    /// <param name="claim">The claim the pass holds.</param>
    /// <param name="now">The instant of the renewal.</param>
    /// <param name="timeout">How long the claim stands from it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The claim as renewed, or nothing where it has been taken over.</returns>
    ValueTask<DeliveryClaim?> RenewAsync(
        DeliveryClaim claim,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes one subscriber's confirmation as it happens, only where the row still
    /// carries the claim.
    /// </summary>
    /// <param name="claim">The claim the pass holds.</param>
    /// <param name="subscriber">What the subscriber is called.</param>
    /// <param name="at">When it confirmed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it was written: not where the claim has been taken over.</returns>
    ValueTask<bool> ConfirmAsync(
        DeliveryClaim claim,
        string subscriber,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes what a pass made of one delivery (its attempts, its schedule, its status)
    /// and releases the claim, by one update conditional on the claim.
    /// </summary>
    /// <param name="delivery">The delivery, as the pass left it.</param>
    /// <param name="claim">The claim the pass was made under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it was written: not where the claim has been taken over.</returns>
    ValueTask<bool> RecordAsync(Delivery delivery, DeliveryClaim claim, CancellationToken cancellationToken);

    /// <summary>
    /// Releases a claim and changes nothing else of the row, by one update conditional
    /// on the claim.
    /// </summary>
    /// <param name="claim">The claim the pass holds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it was released: not where the claim has been taken over.</returns>
    ValueTask<bool> ReleaseAsync(DeliveryClaim claim, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one delivery under a lock on its row held until the operation's
    /// transaction ends, so a decision on it cannot race another (CONV-DESIGN-003).
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The delivery as committed when the lock was taken, or nothing where no such row exists.</returns>
    /// <exception cref="System.InvalidOperationException">No transaction is open.</exception>
    ValueTask<Delivery?> FindForUpdateAsync(DeliveryId delivery, CancellationToken cancellationToken);

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
    /// One page of the erasures that are complete and hold no confirmation from the
    /// off-host ledger, oldest first: those completed before a ledger was registered,
    /// by hand included.
    /// </summary>
    /// <param name="skip">How many of the oldest such erasures to pass over.</param>
    /// <param name="take">How many to read at most.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the deliveries are held under, oldest first.</returns>
    ValueTask<IReadOnlyList<DeliveryId>> UnledgeredAsync(
        int skip,
        int take,
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
