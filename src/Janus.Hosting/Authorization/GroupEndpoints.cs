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
/// Implements AUTHZ-GROUP-001, OPS-CFG-007, LIB-API-005, CONV-CODE-006 and
/// CONV-DESIGN-006. Each is one line to <see cref="IGroups"/>, which judges the
/// permission, the step-up and what the reason says; a body missing a member it
/// requires is refused before it is called.
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

        _ = SessionRequired.On(endpoints.MapGet("/admin/groups", InAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied)
                .Binding<OrganizationId>("organization"));
        _ = SessionRequired.On(endpoints.MapPost("/admin/groups", CreateAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted));
        _ = SessionRequired.On(endpoints.MapDelete("/admin/groups/{id}", RemoveAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.GroupInUse)
                .Binding<GroupId>("id"));
        _ = SessionRequired.On(endpoints.MapPost("/admin/groups/{id}/members", AddMemberAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.StepUpRequired, ErrorCodes.GroupCycle, ErrorCodes.RequestInvalid)
                .Binding<GroupId>("id"));
        _ = SessionRequired.On(endpoints.MapDelete("/admin/groups/{id}/members", RemoveMemberAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.StepUpRequired, ErrorCodes.GroupCycle, ErrorCodes.RequestInvalid)
                .Binding<GroupId>("id"));

        return endpoints;
    }

    private static async Task<IResult> InAsync(
        IGroups groups,
        RequestSession browser,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await groups
                .InAsync(
                    browser.Asking,
                    organization,
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

        // API-CONV-002, X4: free text is 1 to 1024 characters after trimming, refused
        // before the service is called (CONV-CODE-006 AC2).
        if (body.Name?.Trim() is not { Length: > 0 and <= 1024 } name)
        {
            return Answers.Malformed("name");
        }

        if (body.Reason?.Trim() is not { Length: > 0 and <= 1024 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await groups
                .CreateAsync(
                    browser.Asking,
                    new OrganizationId(organization),
                    name,
                    reason,
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
        GroupId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(browser);

        if (body.Reason?.Trim() is not { Length: > 0 and <= 1024 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await groups
                .RemoveAsync(
                    browser.Asking,
                    id,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> AddMemberAsync(
        MemberBody body,
        IGroups groups,
        RequestSession browser,
        GroupId id,
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

        if (body.Reason?.Trim() is not { Length: > 0 and <= 1024 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await groups
                .AddMemberAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    joining,
                    reason,
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
        GroupId id,
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

        if (body.Reason?.Trim() is not { Length: > 0 and <= 1024 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await groups
                .RemoveMemberAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    leaving,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
