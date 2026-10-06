using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.BreakGlass;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.BreakGlass;

/// <summary>
/// The break-glass endpoints of chapter 09 section 8.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-002, OPS-BOOT-004, FE-BG-001 and CONV-DESIGN-006. Presentation
/// is on the machine profile and reads no session, so a stale cookie for the domain
/// neither helps nor refuses it; the session it opens is written as a sign-in writes
/// one. Generation answers the code once and keeps nothing of it; the standing read
/// answers whether one stands and nothing of it (OPS-BOOT-001 AC3).
/// </remarks>
internal static class BreakGlassEndpoints
{
    private static readonly IResult Opened = TypedResults.Ok();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapBreakGlass(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapPost("/auth/break-glass", PresentAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.BreakGlassConsumed,
                    ErrorCodes.BreakGlassInvalid, ErrorCodes.Throttled))
            .Produces(StatusCodes.Status200OK);
        _ = SessionRequired.On(endpoints.MapPost("/admin/break-glass/generate", GenerateAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.Restricted, ErrorCodes.StepUpRequired))
            .Produces<GeneratedBreakGlassView>();
        _ = SessionRequired.On(endpoints.MapGet("/admin/break-glass", StandingAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Denied))
            .Produces<BreakGlassStandingView>();

        return endpoints;
    }

    private static async Task<IResult> PresentAsync(
        PresentBreakGlassRequest request,
        BreakGlassService breakGlass,
        BrowserSessionCookies cookies,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(breakGlass);
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(context);

        if (request.Credential is not { Length: > 0 } credential)
        {
            return Answers.Malformed("credential");
        }

        // API-CONV-002: the reason is free text, 1 to 1024 characters after trimming,
        // and is refused before the credential is looked at (OPS-BOOT-002).
        if (request.Reason?.Trim() is not { Length: > 0 and <= 1024 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await breakGlass
                .PresentAsync(credential, reason, RequestOrigin.Of(context.Request), cancellationToken)
                .ConfigureAwait(false),
            issued =>
            {
                cookies.Write(context.Response, issued);
                cookies.ClearFirstContact(context.Response);

                return Opened;
            });
    }

    private static async Task<IResult> GenerateAsync(
        IBreakGlass breakGlass,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(breakGlass);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await breakGlass
                .GenerateAsync(browser.Asking, browser.Required.Id, cancellationToken)
                .ConfigureAwait(false),
            generated => TypedResults.Json(
                new GeneratedBreakGlassView(
                    generated.Credential,
                    generated.Address.AbsoluteUri,
                    generated.IssuedAt),
                BreakGlassJson.Default.GeneratedBreakGlassView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> StandingAsync(
        IBreakGlass breakGlass,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(breakGlass);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await breakGlass.StandingAsync(browser.Asking, cancellationToken).ConfigureAwait(false),
            issuedAt => TypedResults.Json(
                new BreakGlassStandingView(issuedAt is not null, issuedAt),
                BreakGlassJson.Default.BreakGlassStandingView,
                contentType: null,
                StatusCodes.Status200OK));
    }
}
