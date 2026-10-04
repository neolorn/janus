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
/// Who can access one record, the administrative view of chapter 09 section 8.
/// </summary>
/// <remarks>
/// Implements AUTHZ-DERIVE-007, AUTHZ-GATE-004, LIB-API-005 and CONV-DESIGN-006. It is
/// one line to <see cref="IAccessGate"/>, which judges the permission and evaluates each
/// derivation reaching the record over the rows the host declared a relationship source
/// for (LIB-HOST-001), since no relation of the host's arrives over HTTP (D-166, D-183).
/// </remarks>
internal static class AccessEndpoints
{
    /// <summary>
    /// Mounts it.
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
                .Binding<ResourceId>("resourceId"));

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
}
