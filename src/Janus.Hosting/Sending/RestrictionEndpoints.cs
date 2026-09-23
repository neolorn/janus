using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Sending;

/// <summary>
/// The named restriction set endpoints of chapter 09 section 8: reading the set and one
/// restriction, creating, replacing and deleting one, and granting credit under one.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004, OPS-CFG-002, LIB-API-005 and CONV-DESIGN-006. Each is one
/// line to <see cref="IRestrictionSet"/>, which judges the permission, the step-up and
/// the reason.
/// </remarks>
internal static class RestrictionEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapRestrictions(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/admin/restrictions");

        _ = SessionRequired.On(group.MapGet("/", AllAsync));
        _ = SessionRequired.On(group.MapGet("/{name}", ReadAsync));
        _ = SessionRequired.On(group.MapPut("/{name}", EditAsync));
        _ = SessionRequired.On(group.MapDelete("/{name}", DeleteAsync));
        _ = SessionRequired.On(group.MapPost("/{name}/grant", GrantAsync));

        return endpoints;
    }

    private static async Task<IResult> AllAsync(
        IRestrictionSet restrictions,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(restrictions);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await restrictions
                .AllAsync(AccessContext.Of(browser.Required.Subject), cancellationToken)
                .ConfigureAwait(false),
            all => TypedResults.Json<IReadOnlyList<RestrictionView>>(
                [.. all.Select(RestrictionView.Of)],
                SendingJson.Default.IReadOnlyListRestrictionView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> ReadAsync(
        IRestrictionSet restrictions,
        RequestSession browser,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(restrictions);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await restrictions
                .ReadAsync(AccessContext.Of(browser.Required.Subject), name, cancellationToken)
                .ConfigureAwait(false),
            restriction => TypedResults.Json(
                RestrictionView.Of(restriction),
                SendingJson.Default.RestrictionView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> EditAsync(
        RestrictionBody body,
        IRestrictionSet restrictions,
        RequestSession browser,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(restrictions);
        ArgumentNullException.ThrowIfNull(browser);

        (Restriction? replacement, string member) = body.Read(name);

        if (replacement is null)
        {
            return Answers.Malformed(member);
        }

        return Answers.Of(
            await restrictions
                .EditAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    replacement,
                    body.Reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // A deletion is a loosening and carries its reason, which is free text and goes
    // in the body rather than in an address a log keeps.
    private static async Task<IResult> DeleteAsync(
        [FromBody] RestrictionDeletionBody body,
        IRestrictionSet restrictions,
        RequestSession browser,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(restrictions);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await restrictions
                .DeleteAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    name,
                    body.Reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> GrantAsync(
        RestrictionGrantBody body,
        IRestrictionSet restrictions,
        RequestSession browser,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(restrictions);
        ArgumentNullException.ThrowIfNull(browser);

        if (string.IsNullOrWhiteSpace(body.KeyValue))
        {
            return Answers.Malformed("keyValue");
        }

        if (body.Credit is not int credit)
        {
            return Answers.Malformed("credit");
        }

        return Answers.Of(
            await restrictions
                .GrantAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    name,
                    body.KeyValue,
                    credit,
                    body.Reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
