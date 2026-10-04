using System;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Core;

/// <summary>
/// The one method that registers everything this project provides.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-CODE-007, D-171 and D-176. The key ring and the mail
/// server in use are one of each for the process, and no service receives a secret or a
/// mail server when it is registered or made; each asks at its use.
/// </remarks>
internal static class CoreRegistration
{
    /// <summary>
    /// Registers the key ring and the mail server in use. What fills the ring and
    /// chooses the mail server is the caller's: the application's start, or a command
    /// at its own.
    /// </summary>
    /// <param name="services">The host's collection.</param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    public static IServiceCollection AddCoreArea(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<KeyRing>();
        services.AddSingleton<IKeyRing>(provider => provider.GetRequiredService<KeyRing>());
        services.AddSingleton<MailServerInUse>();
        services.AddSingleton<IMailServerInUse>(provider => provider.GetRequiredService<MailServerInUse>());

        return services;
    }
}
