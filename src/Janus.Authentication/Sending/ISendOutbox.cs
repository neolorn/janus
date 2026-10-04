using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sending;

/// <summary>
/// Where the messages admitted but not yet carried are read and written.
/// </summary>
/// <remarks>
/// Implements D-022, AUTH-ABUSE-004 and CONV-DESIGN-003. The row is added inside the
/// transaction that undertook the message, so nothing here commits: the caller's unit
/// of work does, and a rollback leaves neither the change nor the message. What the row
/// holds is a message in full, so the store keeps it as the storage chapter keeps
/// anything personal, and removes it once the handler has taken it. A row is carried
/// only under a claim, and what an attempt made of it is written only while that claim
/// stands, so two passes over the same rows carry each once.
/// </remarks>
internal interface ISendOutbox
{
    /// <summary>
    /// Writes one admitted message onto the transaction in progress.
    /// </summary>
    /// <param name="delivery">The message.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask AddAsync(SendDelivery delivery, CancellationToken cancellationToken);

    /// <summary>
    /// The messages whose next attempt is due and that no standing claim holds, oldest
    /// first, read without a lock.
    /// </summary>
    /// <param name="now">The instant the pass runs at.</param>
    /// <param name="count">How many at most.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>What each is held under.</returns>
    ValueTask<IReadOnlyList<SendDeliveryId>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims one message for an attempt, by one update that succeeds only where the
    /// row is due, its next attempt's instant come, and is unclaimed or its claim has
    /// timed out. The caller commits it on its own before the handler is called.
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="now">The instant of the claim.</param>
    /// <param name="timeout">How long the claim stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The claim, which the attempt's outcome is written under, or nothing where another
    /// attempt holds the row, the row is not yet due or the row is gone.
    /// </returns>
    ValueTask<SendClaim?> ClaimAsync(
        SendDeliveryId delivery,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads one admitted message.
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The message, or nothing where no such row exists.</returns>
    ValueTask<SendDelivery?> FindAsync(
        SendDeliveryId delivery,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether one admitted message still waits to be carried.
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Whether its row stands.</returns>
    ValueTask<bool> WaitsAsync(SendDeliveryId delivery, CancellationToken cancellationToken);

    /// <summary>
    /// Whether erasure has overwritten the key of one admitted message, which is then
    /// unreadable and is removed without being carried (PRIV-RIGHT-005a).
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// The hash of the reference the message was counted under, which its removal
    /// releases, or nothing where the row is readable or gone.
    /// </returns>
    ValueTask<byte[]?> ErasedAsync(SendDeliveryId delivery, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a message the handler has taken, or one that failed for good, by one
    /// statement that changes nothing where the claim has been taken over.
    /// </summary>
    /// <param name="claim">The claim the attempt was made under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the row was removed under the claim.</returns>
    ValueTask<bool> RemoveAsync(SendClaim claim, CancellationToken cancellationToken);

    /// <summary>
    /// Writes what a failed attempt made of one message, the attempts and the next one,
    /// and gives up the claim, by one update that changes nothing where the claim has
    /// been taken over.
    /// </summary>
    /// <param name="delivery">The message, as the attempt left it.</param>
    /// <param name="claim">The claim the attempt was made under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the outcome was written under the claim.</returns>
    ValueTask<bool> RecordAsync(SendDelivery delivery, SendClaim claim, CancellationToken cancellationToken);
}
