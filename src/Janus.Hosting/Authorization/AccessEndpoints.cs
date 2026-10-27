using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Authorization;

/// <summary>
/// Who can access one record, the administrative view of chapter 09 section 8, and which
/// accounts may exercise a permission on it, of section 8a.
/// </summary>
/// <remarks>
/// Implements AUTHZ-DERIVE-007, AUTHZ-GATE-004, AUTHZ-GATE-007, LIB-API-005 and
/// CONV-DESIGN-006. Each is one line to <see cref="IAccessGate"/>, which judges the
/// permission and evaluates each derivation reaching the record over the rows the host
/// declared a relationship source for (LIB-HOST-001), since no relation of the host's
/// arrives over HTTP (D-166, D-183).
/// </remarks>
internal static class AccessEndpoints
{
    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapAccess(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapGet("/admin/access", WhoCanAccessAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied)
                .Binding<ResourceType>("resourceType")
                .Binding<ResourceId>("resourceId"))
            .Produces<ResourceAccessView>();
        _ = SessionRequired.On(endpoints.MapGet("/admin/access/admitted", WhoMayAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied)
                .Binding<ResourceType>("type")
                .Binding<ResourceId>("id"))
            .Produces<AdmittedAccountsView>();

        return endpoints;
    }

    private static async Task<IResult> WhoCanAccessAsync(
        IAccessGate gate,
        RequestSession browser,
        ResourceType resourceType,
        ResourceId resourceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await gate
                .WhoCanAccessAsync(
                    browser.Asking,
                    new ResourceReference(resourceType, resourceId),
                    cancellationToken)
                .ConfigureAwait(false),
            access => TypedResults.Json(
                ResourceAccessView.Of(access),
                AuthorizationJson.Default.ResourceAccessView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    // 10 section 1, api.request.malformed: a value that does not read as a permission is
    // refused here, as a type or an identifier that does not read is by its binding; one
    // the model does not declare is the gate's to refuse (AUTHZ-GATE-007, D-219).
    private static async Task<IResult> WhoMayAsync(
        IAccessGate gate,
        RequestSession browser,
        ResourceType type,
        ResourceId id,
        string? permission,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(browser);

        if (!Permission.TryParse(permission, out Permission asked))
        {
            return Answers.Malformed(nameof(permission));
        }

        return Answers.Of(
            await gate
                .WhoMayAsync(browser.Asking, asked, new ResourceReference(type, id), cancellationToken)
                .ConfigureAwait(false),
            admitted => TypedResults.Json(
                AdmittedAccountsView.Of(admitted),
                AuthorizationJson.Default.AdmittedAccountsView,
                contentType: null,
                StatusCodes.Status200OK));
    }
}
