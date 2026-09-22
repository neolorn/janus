using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Where an event goes once the transaction that made it true has committed.
/// </summary>
/// <remarks>
/// Implements LIB-API-001, CONV-DESIGN-002, CONV-DESIGN-005 and chapter 10 section 5b.
/// Publishing answers for itself: an operation that publishes inside its transaction
/// returns the failure rather than committing a change no consumer was told of, and
/// the publisher leaves an undelivered event for its next attempt.
/// </remarks>
public interface IEvents
{
    /// <summary>
    /// Hands one event to every consumer registered for its kind.
    /// </summary>
    /// <typeparam name="TEvent">The kind of event.</typeparam>
    /// <param name="raised">The event.</param>
    /// <param name="cancellationToken">Abandons the delivery.</param>
    /// <returns>Nothing, or the failure where the event was not taken.</returns>
    ValueTask<Result> PublishAsync<TEvent>(TEvent raised, CancellationToken cancellationToken)
        where TEvent : JanusEvent;
}
