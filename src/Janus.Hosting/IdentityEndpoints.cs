using System;
using Janus.Hosting.Accounts;
using Janus.Hosting.Authentication;
using Janus.Hosting.Bff;
using Janus.Hosting.Credentials;
using Janus.Hosting.Privacy;
using Janus.Hosting.Recovery;
using Janus.Hosting.Registration;
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

        _ = endpoints.MapRegistration();
        _ = endpoints.MapAuthentication();
        _ = endpoints.MapSignOn();
        _ = endpoints.MapAccount();
        _ = endpoints.MapCredentials();
        _ = endpoints.MapRecovery();
        _ = endpoints.MapPrivacy();
        _ = endpoints.MapTakedowns();

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
