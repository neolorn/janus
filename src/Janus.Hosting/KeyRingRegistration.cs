using System;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Hosting;

/// <summary>
/// How the key ring and the mail server in use are put together: one of each for the
/// process.
/// </summary>
/// <remarks>
/// Implements CONV-CODE-007, CONV-DESIGN-007, D-171 and D-176. Both are singletons, and
/// no service receives a secret or a mail server when it is registered or made; each
/// asks at its use.
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
}
