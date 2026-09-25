using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Sessions;

/// <summary>
/// The two revocations of chapter 09 section 8: one account's sessions, and every
/// session in the deployment.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-009, AUTH-SESS-011, LIB-API-005 and CONV-DESIGN-006. Each is
/// one line to <see cref="ISessions"/>, which judges the permission, so a host calling
/// it in process meets the same refusal.
/// </remarks>
internal static class SessionRevocationEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapSessionRevocation(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapPost("/admin/accounts/{subject:guid}/sessions/revoke", RevokeAccountAsync));
        _ = SessionRequired.On(endpoints.MapPost("/admin/sessions/revoke-all", RevokeEveryAsync));

        return endpoints;
    }

    private static async Task<IResult> RevokeAccountAsync(
        ISessions sessions,
        RequestSession browser,
        Guid subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await sessions
                .RevokeAccountAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new SubjectId(subject),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> RevokeEveryAsync(
        ISessions sessions,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await sessions
                .RevokeEveryAsync(AccessContext.Of(browser.Required.Subject), cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
