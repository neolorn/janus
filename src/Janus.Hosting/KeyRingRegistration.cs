using System;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Janus.Hosting;

/// <summary>
/// How the key ring is put together in a deployment: one ring, and the reading that
/// fills it ahead of every other hosted service.
/// </summary>
/// <remarks>
/// Implements CONV-CODE-007, CONV-DESIGN-007 and D-171. The ring is a singleton, and no
/// service receives a secret when it is registered or made; each borrows at its use.
/// </remarks>
internal static class KeyRingRegistration
{
    /// <summary>
    /// Adds the key ring and puts its reading at the head of the hosted services, so it
    /// starts before every one registered so far and stops after them.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    public static IServiceCollection AddKeyRing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<KeyRing>();
        services.AddSingleton<IKeyRing>(provider => provider.GetRequiredService<KeyRing>());
        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, KeyRingService>());

        return services;
    }
}
