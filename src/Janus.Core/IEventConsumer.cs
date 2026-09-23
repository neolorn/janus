using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What a consumer registers to receive one kind of event. Delivery is at least
/// once, so a handler produces the same result for an event it has already seen,
/// and a handler that did not do its work says so rather than throwing.
/// </summary>
/// <typeparam name="TEvent">The kind of event.</typeparam>
/// <remarks>
/// Implements LIB-API-001, IDN-LIFE-003a. Adding a consumer requires no library
/// change.
/// </remarks>
public interface IEventConsumer<in TEvent>
    where TEvent : DomainEvent
{
    /// <summary>
    /// Acts on one event.
    /// </summary>
    /// <param name="raised">The event.</param>
    /// <param name="cancellationToken">Abandons the work.</param>
    /// <returns>
    /// Whether the consumer did its work. A failure is delivered again; chapter 10
    /// section 5 requires several of these events of every registered handler.
    /// </returns>
    ValueTask<Result> HandleAsync(TEvent raised, CancellationToken cancellationToken);
}
