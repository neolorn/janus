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
/// Implements AUTH-ABUSE-004, OPS-CFG-002, LIB-API-005, CONV-CODE-006 and
/// CONV-DESIGN-006. Each is one line to <see cref="IRestrictionSet"/>, which judges the
/// permission, the step-up and what the reason says; a body missing a member it
/// requires is refused before it is called. A restriction is named by
/// <see cref="RestrictionName"/>, bound from the route, so a name outside its rule is
/// answered before any body is read on every route that takes one (INT-SMS-003,
/// chapter 09 section 8).
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

        _ = SessionRequired.On(group.MapGet("/", AllAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Denied))
            .Produces<IReadOnlyList<RestrictionView>>();
        _ = SessionRequired.On(group.MapGet("/{name}", ReadAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.RestrictionNotFound)
                .Binding<RestrictionName>("name"))
            .Produces<RestrictionView>();
        _ = SessionRequired.On(group.MapPut("/{name}", EditAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.StepUpRequired, ErrorCodes.ConfigurationValueNotAllowed,
                    ErrorCodes.ConfigurationChangeReasonRequired)
                .Binding<RestrictionName>("name"))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapDelete("/{name}", DeleteAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.StepUpRequired, ErrorCodes.RestrictionNotFound,
                    ErrorCodes.ConfigurationChangeReasonRequired)
                .Binding<RestrictionName>("name"))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapPost("/{name}/grant", GrantAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.StepUpRequired, ErrorCodes.RestrictionNotFound,
                    ErrorCodes.ConfigurationChangeReasonRequired,
                    ErrorCodes.ConfigurationValueNotAllowed)
                .Binding<RestrictionName>("name"))
            .Produces(StatusCodes.Status204NoContent);

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
                .AllAsync(browser.Asking, cancellationToken)
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
        RestrictionName name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(restrictions);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await restrictions
                .ReadAsync(browser.Asking, name, cancellationToken)
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
        RestrictionName name,
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

        if (Overlong(body.Reason))
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await restrictions
                .EditAsync(
                    browser.Asking,
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
        RestrictionName name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(restrictions);
        ArgumentNullException.ThrowIfNull(browser);

        if (Overlong(body.Reason))
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await restrictions
                .DeleteAsync(
                    browser.Asking,
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
        RestrictionName name,
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

        // AUTH-ABUSE-004: every grant carries a reason, and chapter 10 names the refusal
        // of one without, so an absent one is answered by it rather than as malformed.
        if (body.Reason?.Trim() is not { Length: > 0 } reason)
        {
            return Answers.Refused(Error.From(ErrorCodes.ConfigurationChangeReasonRequired));
        }

        if (reason.Length > 1024)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await restrictions
                .GrantAsync(
                    browser.Asking,
                    browser.Required.Id,
                    name,
                    body.KeyValue,
                    credit,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // API-CONV-002, X4: a reason past 1024 characters after trimming is a request the
    // boundary does not read, refused before the service is called (CONV-CODE-006 AC2).
    private static bool Overlong(string? reason) => reason?.Trim().Length > 1024;
}
