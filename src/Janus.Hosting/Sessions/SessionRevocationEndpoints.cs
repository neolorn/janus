using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Accounts;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Sessions;

/// <summary>
/// The two revocations of chapter 09 section 8, one account's sessions and every
/// session in the deployment, and the read of one account's sessions of section 8a.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-009, AUTH-SESS-011, IDN-READ-003, LIB-API-005 and
/// CONV-DESIGN-006. Each is one line to <see cref="ISessions"/>, which judges the
/// permission, so a host calling it in process meets the same refusal.
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

        _ = SessionRequired.On(endpoints.MapGet("/admin/accounts/{subject}/sessions", ListOfAsync))
            .Declares(EndpointDeclaration
                .Answering(ErrorCodes.Denied, ErrorCodes.AccountNotFound)
                .Binding<SubjectId>("subject"))
            .Produces<IReadOnlyList<SessionView>>();
        _ = SessionRequired.On(endpoints.MapPost("/admin/accounts/{subject}/sessions/revoke", RevokeAccountAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.Restricted, ErrorCodes.StepUpRequired,
                    ErrorCodes.AccountNotFound)
                .Binding<SubjectId>("subject"))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(endpoints.MapPost("/admin/sessions/revoke-all", RevokeEveryAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.Restricted, ErrorCodes.StepUpRequired))
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static async Task<IResult> ListOfAsync(
        ISessions sessions,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await sessions
                .ListOfAsync(browser.Asking, subject, cancellationToken)
                .ConfigureAwait(false),
            held => TypedResults.Json<IReadOnlyList<SessionView>>(
                [.. held.Select(SessionView.Of)],
                AccountJson.Default.IReadOnlyListSessionView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> RevokeAccountAsync(
        ISessions sessions,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await sessions
                .RevokeAccountAsync(
                    browser.Asking,
                    browser.Required.Id,
                    subject,
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
                .RevokeEveryAsync(browser.Asking, browser.Required.Id, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
