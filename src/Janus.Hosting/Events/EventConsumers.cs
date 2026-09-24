using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Hosting.Events;

/// <summary>
/// The consumers the host registered for the kind of one event.
/// </summary>
/// <param name="services">Where the host's registrations are read.</param>
/// <remarks>
/// Implements LIB-API-001 and chapter 10 section 5b. A consumer is registered as
/// <see cref="IEventConsumer{TEvent}"/> of the kind it takes, and is known by its type,
/// so adding one requires no library change and a retry recognises the ones that have
/// already taken the event.
/// </remarks>
internal sealed class EventConsumers(IServiceProvider services)
{
    /// <summary>
    /// The consumers of one event.
    /// </summary>
    /// <param name="raised">The event.</param>
    /// <returns>Each consumer registered for its kind, bound to it.</returns>
    /// <exception cref="ArgumentNullException">The event is absent.</exception>
    /// <exception cref="InvalidOperationException">The event is not one the library emits.</exception>
    public IReadOnlyList<EventConsumer> Of(DomainEvent raised)
    {
        ArgumentNullException.ThrowIfNull(raised);

        return Bound(raised);
    }

    private List<EventConsumer> Bound(DomainEvent raised) => raised switch
    {
        AccountDeletionCancelled each => Registered(each),
        AccountDeletionRequested each => Registered(each),
        AccountReactivated each => Registered(each),
        AccountRegistered each => Registered(each),
        AccountSuspended each => Registered(each),
        AlertRaised each => Registered(each),
        ConsentChanged each => Registered(each),
        CredentialEnrolled each => Registered(each),
        CredentialInvalidated each => Registered(each),
        CredentialRestored each => Registered(each),
        CredentialSuspended each => Registered(each),
        DeviceVerified each => Registered(each),
        ErasureRequested each => Registered(each),
        ExportRequested each => Registered(each),
        IdentifierAdded each => Registered(each),
        IdentifierPrimaryChanged each => Registered(each),
        IdentifierRemoved each => Registered(each),
        MembershipChanged each => Registered(each),
        NotificationRequested each => Registered(each),
        ObjectionChanged each => Registered(each),
        OrganizationErased each => Registered(each),
        RestrictionChanged each => Registered(each),
        SendingRestrictionChanged each => Registered(each),
        SendingRestrictionGranted each => Registered(each),
        TakedownExecuted each => Registered(each),
        TakedownReversed each => Registered(each),
        _ => throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"'{raised.GetType().Name}' is not an event the library emits.")),
    };

    private List<EventConsumer> Registered<TEvent>(TEvent raised)
        where TEvent : DomainEvent =>
    [
        .. services.GetServices<IEventConsumer<TEvent>>().Select(consumer => new EventConsumer(
            consumer.GetType().FullName ?? consumer.GetType().Name,
            cancellationToken => consumer.HandleAsync(raised, cancellationToken))),
    ];
}
