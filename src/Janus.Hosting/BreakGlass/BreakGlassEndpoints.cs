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
/// one. Generation answers the code once and keeps nothing of it.
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

        _ = endpoints.MapPost("/auth/break-glass", PresentAsync);
        _ = SessionRequired.On(endpoints.MapPost("/admin/break-glass/generate", GenerateAsync));

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

        return Answers.Of(
            await breakGlass
                .PresentAsync(credential, RequestOrigin.Of(context.Request), cancellationToken)
                .ConfigureAwait(false),
            issued =>
            {
                cookies.Write(context.Response, issued);
                cookies.ClearFirstContact(context.Response);

                return Opened;
            });
    }

    private static async Task<IResult> GenerateAsync(
        BreakGlassService breakGlass,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(breakGlass);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await breakGlass
                .GenerateAsync(AccessContext.Of(browser.Required.Subject), browser.Required.Id, cancellationToken)
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
}
