using System;
using Janus.Authorization.Gate;
using Janus.Authorization.Grants;
using Janus.Authorization.Groups;
using Janus.Authorization.Model;
using Janus.Authorization.Resources;
using Janus.Authorization.Roles;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Authorization;

/// <summary>
/// The one method that registers everything this project provides.
/// </summary>
/// <remarks>Implements CONV-DESIGN-007.</remarks>
internal static class AuthorizationRegistration
{
    /// <summary>
    /// Registers the model built from the host's declaration, what the gate reads, and
    /// the services of the grant, role, group and resource operations.
    /// </summary>
    /// <param name="services">The host's collection.</param>
    /// <param name="declaration">What the host declared about its own domain.</param>
    /// <returns>The collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    /// <exception cref="StartupException">The declaration does not hold together.</exception>
    /// <remarks>
    /// The model is built and checked here, once, so a declaration that does not hold
    /// together stops the deployment rather than the first request that reads it
    /// (AUTHZ-MODEL-004).
    /// </remarks>
    public static IServiceCollection AddAuthorizationArea(
        this IServiceCollection services,
        AuthorizationDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(AuthorizationModel.Of(declaration));
        services.AddScoped<ModelValidation>();

        // AUTHZ-GROUP-002: one set per operation, which is what makes ten checks in one
        // request resolve membership once.
        services.AddScoped<SubjectSets>();

        // LIB-HOST-004: the assurance provider is the host's to supply, and a host
        // that supplies none is one where nothing reports what a session has proved
        // other than the library's own session (AUTH-STEP-002).
        services.AddScoped(provider => new StepUpGates(
            provider.GetRequiredService<ISessionGates>(),
            provider.GetService<IAssuranceProvider>(),
            provider.GetRequiredService<TimeProvider>()));

        // OPS-ALERT-006: an export is gated, limited and recorded inside the gate, so no
        // host path exercises one around it.
        services.AddScoped<ExportOperations>();

        services.AddScoped<Derivations>();
        services.AddScoped<ReverseLookup>();
        services.AddScoped<DenialSpikes>();
        services.AddScoped<IDerivationMaterialiser, DerivationMaterialiser>();

        // AUTHZ-INHERIT-002: the host says where each of its records sits, and the
        // ancestry the gate reads is written from that and nothing else.
        services.AddScoped<IResources, ResourceService>();

        // OPS-ALERT-005: the host says how many records a filtered query of its own
        // returned, and the library counts them against the person given them.
        services.AddScoped<ReadVolume>();
        services.AddScoped<IReadVolume>(provider => provider.GetRequiredService<ReadVolume>());
        services.AddScoped<AdministrativeScope>();
        services.AddScoped<IGrants, GrantService>();
        services.AddScoped<IRoles, RoleService>();
        services.AddScoped<IGroups, GroupService>();

        return services;
    }
}
