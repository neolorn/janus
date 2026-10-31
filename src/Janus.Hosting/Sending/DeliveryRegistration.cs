using System;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Hosting.Sending;

/// <summary>
/// How outbound delivery is put together: the one path every message takes to the
/// host's notification handler, and the startup check that the host registered one.
/// </summary>
/// <remarks>
/// Implements INT-SMS-006, INT-MAIL-008 and CONV-DESIGN-007 (D-225). Outbound delivery
/// is a concern of its own, apart from mailbox hosting, so its registration is kept in a
/// file that names only its side. A handler the host did not register reaches the check
/// that refuses its absence through a factory that asks the container for it, never
/// through a parameter whose default stands for it (D-180).
/// </remarks>
internal static class DeliveryRegistration
{
    /// <summary>
    /// Adds the sending path and the startup check of the sending path.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    public static IServiceCollection AddOutboundDelivery(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // AUTH-ABUSE-004, INF-BG-001: what carries the admitted messages, as the one
        // attempt that follows a commit and as the passes of the publisher.
        services.AddScoped<SendPublisher>();
        services.AddScoped<ISendCarrier>(provider => provider.GetRequiredService<SendPublisher>());

        // INT-SMS-006, OPS-ALERT-003: a handler the host did not register is refused at
        // the start by name.
        services.AddScoped(provider => new SendingValidation(
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<RestrictionKeySuppliers>(),
            provider.GetService<INotificationHandler>(),
            provider.GetService<IAlertHandler>()));

        return services;
    }
}
