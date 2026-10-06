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
/// The runtime role management of chapter 09 section 8: reading the roles, defining
/// one and removing one.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004, OPS-CFG-007, LIB-API-005, CONV-CODE-006 and
/// CONV-DESIGN-006. Each is one line to <see cref="IRoles"/>, which judges the
/// permission, the step-up and what the reason says; a body missing a member it
/// requires is refused before it is called.
/// </remarks>
internal static class RoleEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    private static readonly IResult Made = TypedResults.StatusCode(StatusCodes.Status201Created);

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapRoles(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapGet("/admin/roles", AllAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Denied))
            .Produces<IReadOnlyList<RoleView>>();
        _ = SessionRequired.On(endpoints.MapPost("/admin/roles", DefineAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.StepUpRequired))
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(endpoints.MapDelete("/admin/roles/{name}", RemoveAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.StepUpRequired, ErrorCodes.RoleNotFound, ErrorCodes.RoleInUse)
                .Binding<RoleName>("name"))
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static async Task<IResult> AllAsync(
        IRoles roles,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await roles
                .AllAsync(browser.Asking, cancellationToken)
                .ConfigureAwait(false),
            all => TypedResults.Json<IReadOnlyList<RoleView>>(
                [.. all.Select(RoleView.Of)],
                AuthorizationJson.Default.IReadOnlyListRoleView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    // A role that is new is answered as created, and a change to one that stood is
    // answered with nothing.
    private static async Task<IResult> DefineAsync(
        RoleBody body,
        IRoles roles,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(browser);

        (DefinedRole? role, string member) = body.Read();

        if (role is null)
        {
            return Answers.Malformed(member);
        }

        // API-CONV-002, X4: the reason is free text, 1 to 1024 characters after
        // trimming, refused before the service is called (CONV-CODE-006 AC2).
        if (body.Reason?.Trim() is not { Length: > 0 and <= 1024 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await roles
                .DefineAsync(
                    browser.Asking,
                    browser.Required.Id,
                    role,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            created => created ? Made : Nothing);
    }

    // A removal carries its reason, which is free text and goes in the body rather
    // than in an address a log keeps.
    private static async Task<IResult> RemoveAsync(
        [FromBody] RoleRemovalBody body,
        IRoles roles,
        RequestSession browser,
        RoleName name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(browser);

        // API-CONV-002, X4: the reason is free text, 1 to 1024 characters after
        // trimming, refused before the service is called (CONV-CODE-006 AC2).
        if (body.Reason?.Trim() is not { Length: > 0 and <= 1024 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await roles
                .RemoveAsync(
                    browser.Asking,
                    browser.Required.Id,
                    name,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
