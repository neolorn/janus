using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Bff;

/// <summary>
/// The two routes this application's half of the sign-on answers.
/// </summary>
/// <remarks>
/// Implements BFF-SESS-006, BFF-SESS-003 and BFF-OWN-001. Both are safe methods
/// reached as top-level navigations, so neither carries a synchronizer token and what
/// stands in its place on the return is the state the browser was sent out with, bound
/// to the pre-authentication session (BFF-CSRF-005a). They are mounted with the rest of
/// the library, behind the same pipeline, and a host adds nothing to make them work.
/// </remarks>
internal static class SignOnEndpoints
{
    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapSignOn(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapGet(SignOn.StartPath, StartAsync);
        _ = endpoints.MapGet(SignOn.ReturnPath, ReturnAsync);

        return endpoints;
    }

    private static Task<IResult> StartAsync(
        SignOn signOn,
        HttpContext context,
        string? returnTo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signOn);

        return signOn.StartAsync(context, returnTo, cancellationToken);
    }

    private static Task<IResult> ReturnAsync(
        SignOn signOn,
        HttpContext context,
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signOn);

        return signOn.ReturnAsync(context, code, state, error, cancellationToken);
    }
}
