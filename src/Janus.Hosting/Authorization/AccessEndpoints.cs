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
/// one line to <see cref="IAccessGate"/>, which judges the permission and refuses a
/// type a derivation reaches, since no relation of the host's arrives over HTTP
/// (entry 265).
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

        _ = SessionRequired.On(endpoints.MapGet("/admin/access", WhoCanAccessAsync));

        return endpoints;
    }

    private static async Task<IResult> WhoCanAccessAsync(
        IAccessGate gate,
        RequestSession browser,
        string? resourceType,
        string? resourceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(browser);

        if (!ResourceType.TryParse(resourceType, out ResourceType type))
        {
            return Answers.Malformed("resourceType");
        }

        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return Answers.Malformed("resourceId");
        }

        return Answers.Of(
            await gate
                .WhoCanAccessAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new ResourceReference(type, ResourceId.Parse(resourceId)),
                    cancellationToken)
                .ConfigureAwait(false),
            access => TypedResults.Json(
                ResourceAccessView.Of(access),
                AuthorizationJson.Default.ResourceAccessView,
                contentType: null,
                StatusCodes.Status200OK));
    }
}
