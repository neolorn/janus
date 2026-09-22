using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The well-known documents a password manager and an authenticator look for.
/// </summary>
/// <remarks>
/// Implements REG-PM-001, AUTH-FACT-012 and LIB-HOST-003. They sit at the site root
/// by definition and are therefore mounted apart from everything the host puts under a
/// prefix. Both always answer: the addresses they carry are a declaration the
/// deployment cannot start without (LIB-HOST-001).
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
        _ = group.MapGet("/webauthn", RelatedOriginsAsync);

        return endpoints;
    }

    // AUTH-FACT-012: the allowlist is the relying party's own, written from the
    // origins the deployment declared and public as the specification requires.
    private static async Task<IResult> RelatedOriginsAsync(
        IConfigurationStore configuration,
        CancellationToken cancellationToken)
    {
        RelyingParty party = await RelyingParty
            .ForAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Text(party.Allowlist(), "application/json");
    }

    private static RedirectHttpResult ChangePassword(PasskeyAddresses addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        return TypedResults.Redirect(addresses.ChangePassword);
    }

    private static JsonHttpResult<PasskeyEndpointsView> PasskeyEndpoints(PasskeyAddresses addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        return TypedResults.Json(
            new PasskeyEndpointsView(addresses.Enrol, addresses.Manage),
            WellKnownJson.Default.PasskeyEndpointsView,
            contentType: null,
            StatusCodes.Status200OK);
    }
}
