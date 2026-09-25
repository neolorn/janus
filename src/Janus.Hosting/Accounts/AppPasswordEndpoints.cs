using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The mail app password endpoints of chapter 09 section 6.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, LIB-API-005, REG-MAIL-002 and INT-MAIL-010. Each is one
/// line to <see cref="IAppPasswords"/>: the mailbox, the step-up and the token are the
/// service's to judge, so a host calling it in process meets the same refusals. The
/// secret crosses the boundary once, in the answer to the creation.
/// </remarks>
internal static class AppPasswordEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapAppPasswords(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/account/mail/apppasswords");

        _ = SessionRequired.On(group.MapGet("/", ListAsync));
        _ = SessionRequired.On(group.MapPost("/", CreateAsync));
        _ = SessionRequired.On(group.MapDelete("/{id}", RevokeAsync));

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        IAppPasswords passwords,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(passwords);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await passwords
                .ListAsync(AccessContext.Of(browser.Required.Subject), browser.Required.Id, cancellationToken)
                .ConfigureAwait(false),
            held => TypedResults.Json<IReadOnlyList<AppPasswordView>>(
                [.. held.Select(AppPasswordView.Of)],
                AccountJson.Default.IReadOnlyListAppPasswordView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> CreateAsync(
        AppPasswordRequest request,
        IAppPasswords passwords,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(passwords);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        // No secret or hash of it is kept anywhere on the way out, the shared caches a
        // proxy keeps included (INT-MAIL-010 AC4).
        context.Response.Headers.CacheControl = "no-store";

        return request.Label is not string label
            ? Answers.Malformed("label")
            : Answers.Of(
                await passwords
                    .CreateAsync(
                        AccessContext.Of(browser.Required.Subject),
                        browser.Required.Id,
                        label,
                        request.ExpiresAt,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                issued => TypedResults.Json(
                    new IssuedAppPasswordView(issued.Id, issued.Secret),
                    AccountJson.Default.IssuedAppPasswordView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    private static async Task<IResult> RevokeAsync(
        string id,
        IAppPasswords passwords,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(passwords);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        return Answers.Of(
            await passwords
                .RevokeAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    id,
                    RequestOrigin.Source(context.Request),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
