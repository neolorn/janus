using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
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
/// to the pre-authentication session (BFF-CSRF-005a). They are a navigation, so each
/// declares the code chapter 09 gives it to carry in the query member <c>error</c> of
/// its redirect (BFF-ERR-001, CONV-DESIGN-006). They are mounted with the rest of
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

        _ = endpoints.MapGet(SignOn.StartPath, StartAsync)
            .Declares(EndpointDeclaration
                .Answering(ErrorCodes.RequestMalformed, ErrorCodes.SessionCsrfInvalid)
                .Carrying(ErrorCodes.SessionExpired))
            .Produces(StatusCodes.Status302Found);
        _ = endpoints.MapGet(SignOn.ReturnPath, ReturnAsync)
            .Declares(EndpointDeclaration.Answering().Carrying(ErrorCodes.SessionExpired, ErrorCodes.SessionCsrfInvalid))
            .Produces(StatusCodes.Status302Found);

        return endpoints;
    }

    // 09 (D-279): `check` is `true` or absent, and any other value names the member the
    // request cannot be read at.
    private static async Task<IResult> StartAsync(
        SignOn signOn,
        HttpContext context,
        string? returnTo,
        string? check,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signOn);

        if (check is not (null or "true"))
        {
            return Answers.Malformed("check");
        }

        return await signOn.StartAsync(context, returnTo, check is not null, cancellationToken).ConfigureAwait(false);
    }

    private static Task<IResult> ReturnAsync(
        SignOn signOn,
        HttpContext context,
        [NeverLogged] string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signOn);

        return signOn.ReturnAsync(context, code, state, error, cancellationToken);
    }
}
