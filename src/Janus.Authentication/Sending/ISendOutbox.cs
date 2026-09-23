using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sending;

/// <summary>
/// Where the messages undertaken but not yet carried are read and written.
/// </summary>
/// <remarks>
/// Implements D-022 and CONV-DESIGN-003. The row is added inside the transaction that
/// made the message necessary, so nothing here commits: the caller's unit of work
/// does, and a rollback leaves neither the change nor the message. What the row holds
/// is a message in full, so the store keeps it as the storage chapter keeps anything
/// personal, and removes it once a transport has taken it.
/// </remarks>
internal interface ISendOutbox
{
    /// <summary>
    /// Writes one undertaken message onto the transaction in progress.
    /// </summary>
    /// <param name="delivery">The message.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask AddAsync(SendDelivery delivery, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one undertaken message.
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The message, or nothing where no such row exists.</returns>
    ValueTask<SendDelivery?> FindAsync(
        SendDeliveryId delivery,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a message a transport has taken.
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(SendDeliveryId delivery, CancellationToken cancellationToken);
}
