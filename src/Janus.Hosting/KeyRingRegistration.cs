using System;
using Janus.Core;
using Janus.Hosting.Mailboxes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// How the key ring and the mail server in use are put together: one of each for the
/// process.
/// </summary>
/// <remarks>
/// Implements CONV-CODE-007, CONV-DESIGN-007, D-171, D-176 and D-180. Both are
/// singletons, and no service receives a secret or a mail server when it is registered or
/// made; each asks at its use. The host's own mail server and its secret source are
/// declarations it may leave out, so the service that fills the ring is made by a factory
/// that asks the container for each.
/// </remarks>
internal static class KeyRingRegistration
{
    /// <summary>
    /// Adds the key ring and the mail server in use. The service that fills and chooses
    /// them is placed among the hosted services by the caller.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    public static IServiceCollection AddKeyRing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<KeyRing>();
        services.AddSingleton<IKeyRing>(provider => provider.GetRequiredService<KeyRing>());
        services.AddSingleton<MailServerInUse>();
        services.AddSingleton<IMailServerInUse>(provider => provider.GetRequiredService<MailServerInUse>());

        return services;
    }

    /// <summary>
    /// The hosted service that fills the ring and chooses the mail server in use, for the
    /// caller to place among the hosted services.
    /// </summary>
    /// <returns>Its registration, a singleton made by <see cref="Service"/>.</returns>
    public static ServiceDescriptor HostedService() =>
        ServiceDescriptor.Singleton<IHostedService>(Service);

    /// <summary>
    /// Makes the service that fills the ring and chooses the mail server in use.
    /// </summary>
    /// <param name="provider">
    /// The container, asked for the host's own mail server and secret source where it
    /// registered them.
    /// </param>
    /// <returns>The service.</returns>
    /// <exception cref="ArgumentNullException">The container is absent.</exception>
    public static KeyRingService Service(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new KeyRingService(
            provider.GetRequiredService<KeyRing>(),
            provider.GetRequiredService<MailServerInUse>(),
            provider.GetRequiredService<JmapMailServer>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetServices<SocialProvider>(),
            provider.GetService<IMailServer>(),
            provider.GetService<ISecretSource>());
    }
}
