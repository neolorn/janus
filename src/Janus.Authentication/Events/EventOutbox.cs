using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Events;

/// <summary>
/// Where an emitted event goes: onto the transaction that made it true, as a row the
/// publisher offers to every consumer registered for its kind once that transaction
/// has committed.
/// </summary>
/// <param name="events">Where the events wait.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <remarks>
/// Implements LIB-API-001, CONV-DESIGN-002 and D-162 item 29. A consumer is never
/// called here: an operation that rolls back leaves no row, so nothing hears of a change
/// that did not happen, and a consumer that fails is offered the event again by the
/// publisher rather than failing the operation.
/// </remarks>
internal sealed class EventOutbox(IPendingEvents events, IUnitOfWork work) : IEvents
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The event is absent.</exception>
    public async ValueTask<Result> PublishAsync<TEvent>(TEvent raised, CancellationToken cancellationToken)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(raised);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await events.AddAsync(PendingEvent.Of(raised), cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
