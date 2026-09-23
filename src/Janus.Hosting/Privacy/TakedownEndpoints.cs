using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The takedown endpoints of chapter 09 section 8a.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, LIB-API-005, IDN-LIFE-003 and PRIV-MINOR-002. Each is
/// one line to <see cref="ITakedowns"/>; the permission, the step-up and the state are
/// the service's to judge, so a host calling it in process meets the same refusals.
/// </remarks>
internal static class TakedownEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapTakedowns(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/admin/accounts/{subject:guid}/takedown");

        _ = SessionRequired.On(group.MapPost("/", ExecuteAsync));
        _ = SessionRequired.On(group.MapGet("/", ReadAsync));
        _ = SessionRequired.On(group.MapPost("/reverse", ReverseAsync));

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync(
        TakedownBody body,
        ITakedowns takedowns,
        RequestSession browser,
        Guid subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(takedowns);
        ArgumentNullException.ThrowIfNull(browser);

        if (body.Trigger is not TakedownTrigger trigger)
        {
            return Answers.Malformed("trigger");
        }

        if (body.Reason is not { Length: > 0 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await takedowns
                .ExecuteAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new SubjectId(subject),
                    trigger,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            executed => TypedResults.Json(
                ExecutedTakedownView.Of(executed),
                PrivacyJson.Default.ExecutedTakedownView,
                contentType: null,
                StatusCodes.Status202Accepted));
    }

    private static async Task<IResult> ReadAsync(
        ITakedowns takedowns,
        RequestSession browser,
        Guid subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(takedowns);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await takedowns
                .ReadAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new SubjectId(subject),
                    cancellationToken)
                .ConfigureAwait(false),
            progress => TypedResults.Json(
                TakedownProgressView.Of(progress),
                PrivacyJson.Default.TakedownProgressView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> ReverseAsync(
        TakedownReversalBody body,
        ITakedowns takedowns,
        RequestSession browser,
        Guid subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(takedowns);
        ArgumentNullException.ThrowIfNull(browser);

        if (body.Reason is not { Length: > 0 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await takedowns
                .ReverseAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new SubjectId(subject),
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
