using System;
using Janus.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The two well-known documents a password manager looks for.
/// </summary>
/// <remarks>
/// Implements REG-PM-001 and LIB-HOST-003. They sit at the site root by definition
/// and are therefore mounted apart from everything the host puts under a prefix. A
/// deployment that has declared no addresses serves neither, which is what a password
/// manager reads as "this site does not offer that".
/// </remarks>
internal static class WellKnownEndpoints
{
    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapWellKnown(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/.well-known");

        _ = group.MapGet("/change-password", ChangePassword);
        _ = group.MapGet("/passkey-endpoints", PasskeyEndpoints);

        return endpoints;
    }

    private static IResult ChangePassword(PasskeyAddresses addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        return addresses.ChangePassword.Length is 0
            ? TypedResults.NotFound()
            : TypedResults.Redirect(addresses.ChangePassword);
    }

    private static IResult PasskeyEndpoints(PasskeyAddresses addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        return addresses.Enrol.Length is 0 || addresses.Manage.Length is 0
            ? TypedResults.NotFound()
            : TypedResults.Json(
                new PasskeyEndpointsView(addresses.Enrol, addresses.Manage),
                WellKnownJson.Default.PasskeyEndpointsView,
                contentType: null,
                StatusCodes.Status200OK);
    }
}
