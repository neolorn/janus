using System;
using Janus.Authorization.Gate;
using Janus.Authorization.Model;
using Janus.Core;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Janus.Hosting;

/// <summary>
/// The one method a host calls to register the library.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-LAYOUT-002 and AUTHZ-SEAM-001. What the host
/// declares about its own domain is built and checked here, once, so a declaration that
/// does not hold together stops the deployment rather than the first request that reads
/// it (AUTHZ-MODEL-004).
/// </remarks>
public static class JanusRegistration
{
    /// <summary>
    /// Registers the library over the host's database and declared domain.
    /// </summary>
    /// <param name="services">The host's collection.</param>
    /// <param name="connectionString">
    /// The application's own credential, which holds row-level access and no schema
    /// right (OPS-MIG-003).
    /// </param>
    /// <param name="keyEncryptionKeys">
    /// The versions a subject key may be wrapped under, read from the secrets manager
    /// at startup and never from the database (OPS-SEC-001).
    /// </param>
    /// <param name="fingerprintKey">
    /// The key the searchable fingerprints are computed under, read from the same place
    /// and held outside the database (PRIV-RIGHT-005c).
    /// </param>
    /// <param name="declaration">What the host declared about its own domain.</param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    /// <exception cref="StartupException">The declaration does not hold together.</exception>
    public static IServiceCollection AddJanus(
        this IServiceCollection services,
        string connectionString,
        KeyEncryptionKeys keyEncryptionKeys,
        ReadOnlyMemory<byte> fingerprintKey,
        AuthorizationDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // CONV-DESIGN-007: time is injected, and a host that has its own clock keeps it.
        services.TryAddSingleton(TimeProvider.System);

        services.AddJanusStorage(connectionString, keyEncryptionKeys, fingerprintKey);
        services.AddSingleton(AuthorizationModel.Of(declaration));

        // AUTHZ-GROUP-002: one set per operation, which is what makes ten checks in one
        // request resolve membership once.
        services.AddScoped<SubjectSets>();
        services.AddScoped<IAccessGate, AccessGate>();

        return services;
    }
}
