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
/// LIB-API-005, CONV-CODE-006 and CONV-DESIGN-006. Each is one line to
/// <see cref="IGrants"/>, which judges the permission, the step-up and what the reason
/// says; a body missing a member it requires is refused before it is called.
/// </remarks>
internal static class GrantEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    // AUTHZ-GRANT-003: chapter 10 names the refusal of a grant or a revocation
    // without a reason, so an absent one is answered by it rather than as malformed.
    private static readonly Error Unreasoned = Error.From(ErrorCodes.GrantReasonRequired);

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapGrants(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapGet("/admin/grants", HeldAsync))
            .Declares(EndpointDeclaration.Answering().Binding<OrganizationId>("organization"));
        _ = SessionRequired.On(endpoints.MapPost("/admin/grants", GrantAsync));
        _ = SessionRequired.On(endpoints.MapDelete("/admin/grants/{id}", RevokeAsync))
            .Declares(EndpointDeclaration.Answering().Binding<GrantId>("id"));

        return endpoints;
    }

    private static async Task<IResult> HeldAsync(
        IGrants grants,
        RequestSession browser,
        OrganizationId organization,
        string? subjectType,
        string? subjectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(browser);

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
                    browser.Asking,
                    organization,
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

        // API-CONV-002, X4: a reason absent or blank is refused with its own code, and one
        // past 1024 characters after trimming is malformed, both before the service is
        // called (CONV-CODE-006 AC2).
        if (body.Reason?.Trim() is not { Length: > 0 } reason)
        {
            return Answers.Refused(Unreasoned);
        }

        if (reason.Length > 1024)
        {
            return Answers.Malformed("reason");
        }

        (GrantRequest? request, string member) = body.Read(reason);

        if (request is null)
        {
            return Answers.Malformed(member);
        }

        return Answers.Of(
            await grants
                .GrantAsync(
                    browser.Asking,
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
        GrantId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(browser);

        if (body.Reason?.Trim() is not { Length: > 0 } reason)
        {
            return Answers.Refused(Unreasoned);
        }

        if (reason.Length > 1024)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await grants
                .RevokeAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
