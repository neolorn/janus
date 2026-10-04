using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// The governed send as the two callers that act on what became of a message meet it:
/// the alert router, which falls to its second channel where the first carried
/// nothing, and a loss report, whose invalidation waits while no notice was delivered.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004, OPS-ALERT-003 and AUTH-RECOV-007. The message is
/// undertaken like any other, in a unit of work of the caller's own, and its one attempt
/// follows that commit; the caller then asks whether the attempt carried it. What it did
/// not carry is the publisher's, as for every message.
/// </remarks>
internal interface IFollowedSend
{
    /// <summary>
    /// Undertakes one message inside the unit of work in progress, as
    /// <see cref="IGovernedSend.UndertakeAsync"/> does, and answers what was written to
    /// the outbox for it.
    /// </summary>
    /// <param name="message">What is to be sent.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What each admitted message is held under, or the refusal.</returns>
    ValueTask<Result<IReadOnlyList<SendDeliveryId>>> AdmitAsync(
        OutboundMessage message,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether every one of the messages an undertaking wrote has been carried, asked
    /// once the unit of work that undertook them has committed.
    /// </summary>
    /// <param name="admitted">What the undertaking wrote.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Whether none of them still waits in the outbox.</returns>
    ValueTask<bool> CarriedAsync(
        IReadOnlyList<SendDeliveryId> admitted,
        CancellationToken cancellationToken);
}
