using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Janus.Core;

/// <summary>
/// The one method that registers everything this project provides.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-CODE-007, D-171, D-176 and D-187. The key ring and
/// the mail server in use are one of each for the process, and no service receives a
/// secret or a mail server when it is registered or made; each asks at its use.
/// </remarks>
internal static class CoreRegistration
{
    /// <summary>
    /// Registers the key ring, the mail server in use, and the shipped defaults that
    /// stand in for a declaration the host did not make. What fills the ring and
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

        // LIB-HOST-001: a declaration the host may leave out is the host's where it made
        // one, so each default stands only where none is registered. A deployment that
        // declares none of them starts, and what would have read a declaration finds
        // nothing to read.
        services.TryAddSingleton(RestrictionKeySuppliers.None);
        services.TryAddSingleton(PreferenceDeclarations.None);
        services.TryAddSingleton(ReservedUsernames.Default);
        services.TryAddSingleton(DictionaryWords.Default);

        return services;
    }
}
