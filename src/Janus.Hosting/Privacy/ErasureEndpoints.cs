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

namespace Janus.Hosting.Privacy;

/// <summary>
/// The erasure endpoints of chapter 09 section 8a.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, LIB-API-005, IDN-LIFE-003a and IDN-LIFE-003b. Each is one
/// line to <see cref="IErasures"/>; the permission, the step-up and the state are the
/// service's to judge, so a host calling it in process meets the same refusals.
/// </remarks>
internal static class ErasureEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapErasures(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/admin/erasures");

        _ = SessionRequired.On(group.MapGet("/", ListAsync));
        _ = SessionRequired.On(group.MapGet("/{id:guid}", ReadAsync));
        _ = SessionRequired.On(group.MapPost("/{id:guid}/complete", CompleteAsync));

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        IErasures erasures,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(erasures);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await erasures
                .ListAsync(AccessContext.Of(browser.Required.Subject), cancellationToken)
                .ConfigureAwait(false),
            outstanding => TypedResults.Json<IReadOnlyList<ErasureProgressView>>(
                [.. outstanding.Select(ErasureProgressView.Of)],
                PrivacyJson.Default.IReadOnlyListErasureProgressView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> ReadAsync(
        IErasures erasures,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(erasures);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await erasures
                .ReadAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new ErasureId(id),
                    cancellationToken)
                .ConfigureAwait(false),
            progress => TypedResults.Json(
                ErasureProgressView.Of(progress),
                PrivacyJson.Default.ErasureProgressView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> CompleteAsync(
        IErasures erasures,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(erasures);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await erasures
                .CompleteAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new ErasureId(id),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
