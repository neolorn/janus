using System;
using System.Linq;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Erasures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Janus.Hosting.Sending;

/// <summary>
/// How outbound delivery is put together: the one path every message takes, what
/// decides whether it goes, and the startup check that each channel it takes has a
/// transport.
/// </summary>
/// <remarks>
/// Implements INT-MAIL-009, INT-MAIL-008, INT-SMS-006 and CONV-DESIGN-007. Outbound
/// delivery is a concern of its own, apart from mailbox hosting, so its registration is
/// kept in a file that names only its side. A transport the host did not register
/// reaches the check that refuses its absence through a factory that asks the container
/// for it, never through a parameter whose default stands for it (D-180).
/// </remarks>
internal static class DeliveryRegistration
{
    /// <summary>
    /// Adds the sending path, the shipped handler and catalogue where the host
    /// registered none of its own, and the startup check of the sending path.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    public static IServiceCollection AddOutboundDelivery(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // AUTH-ABUSE-004, OPS-ALERT-001: the one path every message takes.
        services.AddScoped<SendingService>();
        services.AddScoped<ISendingRestrictions>(provider => provider.GetRequiredService<SendingService>());

        // LIB-EXT-001: the shipped handler carries email and SMS; a deployment that
        // registers its own before this runs keeps it.
        services.TryAddScoped<INotificationHandler>(
            provider => provider.GetRequiredService<SendingService>());

        // LIB-EXT-001: the shipped catalogue words every message in the languages the
        // library carries, and is likewise kept only where the deployment registered
        // none of its own. A deployment that registers neither still starts.
        services.TryAddSingleton<IMessageTemplates, DefaultMessageTemplates>();

        // INT-MAIL-008, INT-SMS-006: a transport the host did not register is refused at
        // the start by name.
        services.AddScoped(provider => new SendingValidation(
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IMessageTemplates>(),
            provider.GetRequiredService<RestrictionKeySuppliers>(),
            Measured(provider),
            provider.GetService<IMailTransport>(),
            provider.GetService<ISmsTransport>()));

        return services;
    }

    // INT-SMS-003: the subscribers an erasure waits for, the erasure ledger among them
    // where one is registered, the categories the host declared and the origins a link
    // lands on, as this deployment registered and declared them; nothing where it
    // declared no origins, since no link is measured without them.
    private static MessagePlaceholders? Measured(IServiceProvider provider) =>
        provider.GetService<LandingOrigins>() is LandingOrigins landing
            ? new(
                ErasureLedgerSubscriber
                    .Joined(provider.GetServices<ISubjectEventSubscriber>(), provider.GetService<IErasureLedger>())
                    .Where(subscriber => subscriber.Required)
                    .Select(subscriber => subscriber.Name),
                provider.GetRequiredService<AuthorizationDeclaration>().RetentionFloors.Keys,
                landing)
            : null;
}
