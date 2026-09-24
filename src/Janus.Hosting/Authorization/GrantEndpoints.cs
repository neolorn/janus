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

namespace Janus.Hosting.Authorization;

/// <summary>
/// The grant endpoints of chapter 09 section 8: writing a grant and revoking one, and
/// reading what one user or group holds in its own name (entry 268).
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-001, AUTHZ-GRANT-003, AUTHZ-GRANT-004, OPS-CFG-007,
/// LIB-API-005 and CONV-DESIGN-006. Each is one line to <see cref="IGrants"/>, which
/// judges the permission, the step-up and the reason.
/// </remarks>
internal static class GrantEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapGrants(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapGet("/admin/grants", HeldAsync));
        _ = SessionRequired.On(endpoints.MapPost("/admin/grants", GrantAsync));
        _ = SessionRequired.On(endpoints.MapDelete("/admin/grants/{id:guid}", RevokeAsync));

        return endpoints;
    }

    private static async Task<IResult> HeldAsync(
        IGrants grants,
        RequestSession browser,
        string? organization,
        string? subjectType,
        string? subjectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(browser);

        if (!Guid.TryParse(organization, out Guid whose))
        {
            return Answers.Malformed("organization");
        }

        SubjectType? type = subjectType switch
        {
            "user" => SubjectType.User,
            "group" => SubjectType.Group,
            _ => null,
        };

        if (type is not SubjectType holderType)
        {
            return Answers.Malformed("subjectType");
        }

        if (!Guid.TryParse(subjectId, out Guid holder))
        {
            return Answers.Malformed("subjectId");
        }

        return Answers.Of(
            await grants
                .HeldAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new OrganizationId(whose),
                    new GrantSubject(holderType, holder),
                    cancellationToken)
                .ConfigureAwait(false),
            held => TypedResults.Json<IReadOnlyList<HeldGrantView>>(
                [.. held.Select(HeldGrantView.Of)],
                AuthorizationJson.Default.IReadOnlyListHeldGrantView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> GrantAsync(
        GrantBody body,
        IGrants grants,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(browser);

        (GrantRequest? request, string member) = body.Read();

        if (request is null)
        {
            return Answers.Malformed(member);
        }

        return Answers.Of(
            await grants
                .GrantAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    request,
                    cancellationToken)
                .ConfigureAwait(false),
            granted => TypedResults.Json(
                CreatedGrantView.Of(granted),
                AuthorizationJson.Default.CreatedGrantView,
                contentType: null,
                StatusCodes.Status201Created));
    }

    // A revocation carries its reason, which is free text and goes in the body rather
    // than in an address a log keeps.
    private static async Task<IResult> RevokeAsync(
        [FromBody] GrantRevocationBody body,
        IGrants grants,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await grants
                .RevokeAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new GrantId(id),
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
