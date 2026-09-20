using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Where an event goes once the transaction that made it true has committed.
/// </summary>
/// <remarks>
/// Implements LIB-API-001, CONV-DESIGN-002 and chapter 10 section 5b. An operation
/// publishes after the commit, never inside it, so nothing reaches a consumer that
/// the database does not hold.
/// </remarks>
public interface IEvents
{
    /// <summary>
    /// Hands one event to every consumer registered for its kind.
    /// </summary>
    /// <typeparam name="TEvent">The kind of event.</typeparam>
    /// <param name="raised">The event.</param>
    /// <param name="cancellationToken">Abandons the delivery.</param>
    /// <returns>The work of delivering it.</returns>
    ValueTask PublishAsync<TEvent>(TEvent raised, CancellationToken cancellationToken)
        where TEvent : JanusEvent;
}
