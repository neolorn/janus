using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sending;

/// <summary>
/// What makes the one immediate attempt at an admitted message once the transaction
/// that undertook it has committed.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004, CONV-DESIGN-002 and INF-BG-001. The governed send
/// registers the attempt on the unit of work; what the attempt does not carry is the
/// outbox publisher's.
/// </remarks>
internal interface ISendCarrier
{
    /// <summary>
    /// Claims one message just committed and carries it, outside any transaction.
    /// </summary>
    /// <param name="delivery">What its outbox row is held under.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>The work of attempting it.</returns>
    ValueTask AttemptAsync(SendDeliveryId delivery, CancellationToken cancellationToken);
}
