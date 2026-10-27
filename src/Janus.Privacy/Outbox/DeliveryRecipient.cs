using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;

namespace Janus.Privacy.Outbox;

/// <summary>
/// One of those a delivery is owed to: a subject-event subscriber, for an erasure, a
/// restriction change or a takedown, or a purpose handler naming the purpose of a
/// consent or an objection change.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003a, PRIV-RIGHT-001a and PRIV-CONS-008. Both are delivered to,
/// confirmed by name, listed and completed by hand alike, and every purpose handler is
/// required (chapter 10 section 5b).
/// </remarks>
internal sealed class DeliveryRecipient
{
    private readonly ISubjectEventSubscriber? _subscriber;

    private readonly IPurposeHandler? _handler;

    private DeliveryRecipient(
        string name,
        bool required,
        ISubjectEventSubscriber? subscriber,
        IPurposeHandler? handler)
    {
        Name = name;
        Required = required;
        _subscriber = subscriber;
        _handler = handler;
    }

    /// <summary>
    /// What its confirmation is recorded under.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether a delivery waits for it.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Those a delivery is owed to, in the order they are offered it: for an erasure
    /// the ledger's line, where the deployment registered a ledger, then the host's
    /// subscribers; for a restriction change or a takedown the host's subscribers; and
    /// for a consent or an objection change every purpose handler naming its purpose.
    /// </summary>
    /// <param name="delivery">The delivery.</param>
    /// <param name="subscribers">The subject-event subscribers the host registered.</param>
    /// <param name="handlers">The purpose handlers the host registered.</param>
    /// <param name="ledger">The ledger the deployment registered, if any.</param>
    /// <returns>Those it is owed to.</returns>
    /// <exception cref="ArgumentNullException">One is absent.</exception>
    public static IReadOnlyList<DeliveryRecipient> Of(
        Delivery delivery,
        IEnumerable<ISubjectEventSubscriber> subscribers,
        IEnumerable<IPurposeHandler> handlers,
        IErasureLedger? ledger)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(subscribers);
        ArgumentNullException.ThrowIfNull(handlers);

        return delivery.Kind switch
        {
            SubjectEventKind.ConsentChanged or SubjectEventKind.ObjectionChanged =>
            [
                .. handlers
                    .Where(handler => delivery.Purpose is string purpose
                        && handler.Purposes.Contains(purpose, StringComparer.Ordinal))
                    .Select(handler => new DeliveryRecipient(handler.Name, required: true, null, handler)),
            ],
            SubjectEventKind.ErasureRequested =>
            [
                .. ErasureLedgerSubscriber.Joined(subscribers, ledger).Select(Subscribing),
            ],
            _ => [.. subscribers.Select(Subscribing)],
        };
    }

    /// <summary>
    /// A subject-event subscriber as one a delivery is owed to.
    /// </summary>
    /// <param name="subscriber">The subscriber.</param>
    /// <returns>The recipient.</returns>
    /// <exception cref="ArgumentNullException">The subscriber is absent.</exception>
    public static DeliveryRecipient Subscribing(ISubjectEventSubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        return new(subscriber.Name, subscriber.Required, subscriber, null);
    }

    /// <summary>
    /// Offers the delivery's event: the subject event to a subscriber, and the consent
    /// or objection change to a purpose handler.
    /// </summary>
    /// <param name="delivery">The delivery.</param>
    /// <param name="cancellationToken">Abandons the work.</param>
    /// <returns>Whether the recipient did its work.</returns>
    /// <exception cref="ArgumentNullException">The delivery is absent.</exception>
    /// <exception cref="InvalidOperationException">The delivery carries no event this recipient is offered.</exception>
    public ValueTask<Result> HandleAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        return (_subscriber, _handler, delivery.Kind) switch
        {
            (ISubjectEventSubscriber subscriber, _, _) =>
                subscriber.HandleAsync(delivery.Raised(), cancellationToken),
            (_, IPurposeHandler handler, SubjectEventKind.ConsentChanged) =>
                handler.ConsentChangedAsync(delivery.RaisedConsent(), cancellationToken),
            (_, IPurposeHandler handler, _) =>
                handler.ObjectionChangedAsync(delivery.RaisedObjection(), cancellationToken),
            _ => throw new InvalidOperationException("The recipient is neither a subscriber nor a purpose handler."),
        };
    }
}
