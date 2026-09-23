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
/// The group endpoints of chapter 09 section 8a: reading an organization's groups,
/// creating and removing one, and changing its members.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001, OPS-CFG-007, LIB-API-005 and CONV-DESIGN-006. Each is
/// one line to <see cref="IGroups"/>, which judges the permission, the step-up and the
/// reason.
/// </remarks>
internal static class GroupEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapGroups(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapGet("/admin/groups", InAsync));
        _ = SessionRequired.On(endpoints.MapPost("/admin/groups", CreateAsync));
        _ = SessionRequired.On(endpoints.MapDelete("/admin/groups/{id:guid}", RemoveAsync));
        _ = SessionRequired.On(endpoints.MapPost("/admin/groups/{id:guid}/members", AddMemberAsync));
        _ = SessionRequired.On(endpoints.MapDelete("/admin/groups/{id:guid}/members", RemoveMemberAsync));

        return endpoints;
    }

    private static async Task<IResult> InAsync(
        IGroups groups,
        RequestSession browser,
        string? organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(browser);

        if (!Guid.TryParse(organization, out Guid whose))
        {
            return Answers.Malformed("organization");
        }

        return Answers.Of(
            await groups
                .InAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new OrganizationId(whose),
                    cancellationToken)
                .ConfigureAwait(false),
            held => TypedResults.Json<IReadOnlyList<GroupView>>(
                [.. held.Select(GroupView.Of)],
                AuthorizationJson.Default.IReadOnlyListGroupView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> CreateAsync(
        GroupBody body,
        IGroups groups,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(browser);

        if (body.Organization is not Guid organization)
        {
            return Answers.Malformed("organization");
        }

        return Answers.Of(
            await groups
                .CreateAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new OrganizationId(organization),
                    body.Name ?? string.Empty,
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            created => TypedResults.Json(
                CreatedGroupView.Of(created),
                AuthorizationJson.Default.CreatedGroupView,
                contentType: null,
                StatusCodes.Status201Created));
    }

    // A removal carries its reason, which is free text and goes in the body rather
    // than in an address a log keeps.
    private static async Task<IResult> RemoveAsync(
        [FromBody] GroupRemovalBody body,
        IGroups groups,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await groups
                .RemoveAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new GroupId(id),
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> AddMemberAsync(
        MemberBody body,
        IGroups groups,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(browser);

        (GrantSubject? subject, string member) = body.Read();

        if (subject is not GrantSubject joining)
        {
            return Answers.Malformed(member);
        }

        return Answers.Of(
            await groups
                .AddMemberAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new GroupId(id),
                    joining,
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // The member leaving is named in the body, as the one joining is, so the one
    // address serves both.
    private static async Task<IResult> RemoveMemberAsync(
        [FromBody] MemberBody body,
        IGroups groups,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(browser);

        (GrantSubject? subject, string member) = body.Read();

        if (subject is not GrantSubject leaving)
        {
            return Answers.Malformed(member);
        }

        return Answers.Of(
            await groups
                .RemoveMemberAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new GroupId(id),
                    leaving,
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
