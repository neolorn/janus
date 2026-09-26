using System;
using Janus.Core;
using Janus.Hosting.Accounts;
using Janus.Hosting.Authentication;
using Janus.Hosting.Authorization;
using Janus.Hosting.Bff;
using Janus.Hosting.BreakGlass;
using Janus.Hosting.Configuration;
using Janus.Hosting.Credentials;
using Janus.Hosting.Maintenance;
using Janus.Hosting.Organizations;
using Janus.Hosting.Privacy;
using Janus.Hosting.Recovery;
using Janus.Hosting.Registration;
using Janus.Hosting.Sending;
using Janus.Hosting.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting;

/// <summary>
/// The one call a host makes to mount the library's endpoints.
/// </summary>
/// <remarks>
/// Implements API-CONV-001, LIB-HOST-003, LIB-API-005 and CONV-DESIGN-006. Nothing
/// here names an absolute path: the host mounts this wherever it chooses, and every
/// path below is relative to that. What protects these endpoints is that the host
/// mounted the browser profile before them (BFF-ORDER-001).
/// </remarks>
public static class IdentityEndpoints
{
    /// <summary>
    /// Mounts the registration, authentication, account, credential, recovery and
    /// privacy endpoints under the caller's group.
    /// </summary>
    /// <param name="endpoints">Where they are to be mounted.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // BFF-LOG-002, CONV-LOG-003: every body the library answers carries a
        // credential or a person's data, so none of them is logged.
        RouteGroupBuilder library = endpoints
            .MapGroup(string.Empty)
            .WithMetadata(new SensitiveBodyAttribute());

        _ = library.MapRegistration();
        _ = library.MapAuthentication();
        _ = library.MapBreakGlass();
        _ = library.MapSignOn();
        _ = library.MapAccount();
        _ = library.MapAccountAdministration();
        _ = library.MapAppPasswords();
        _ = library.MapCredentials();
        _ = library.MapRecovery();
        _ = library.MapPrivacy();
        _ = library.MapMaintenance();
        _ = library.MapTakedowns();
        _ = library.MapErasures();
        _ = library.MapSessionRevocation();
        _ = library.MapExplanations();
        _ = library.MapAccess();
        _ = library.MapAuditTrail();
        _ = library.MapPublication();
        _ = library.MapConfiguration();
        _ = library.MapRestrictions();
        _ = library.MapDeliveryReports();
        _ = library.MapProviderEvents();
        _ = library.MapGrants();
        _ = library.MapRoles();
        _ = library.MapGroups();
        _ = library.MapOrganizations();

        return endpoints;
    }

    /// <summary>
    /// Mounts the two documents of REG-PM-001, which sit at the site root and not
    /// under any prefix.
    /// </summary>
    /// <param name="endpoints">The application's root.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapIdentityWellKnown(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapWellKnown();
    }
}
