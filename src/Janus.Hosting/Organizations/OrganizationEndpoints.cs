using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Organizations;

/// <summary>
/// The organization lifecycle of chapter 09 section 8a: creating an organization,
/// requesting its deletion and cancelling the request.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-002, IDN-ORG-003, IDN-ORG-004, LIB-API-005 and CONV-DESIGN-006.
/// Each is one line to <see cref="IOrganizations"/>, which judges the permission, the
/// step-up and the reason.
/// </remarks>
internal static class OrganizationEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapOrganizations(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapPost("/admin/organizations", CreateAsync));
        _ = SessionRequired.On(endpoints.MapPost("/admin/organizations/{id:guid}/delete", RequestDeletionAsync));
        _ = SessionRequired.On(endpoints.MapPost("/admin/organizations/{id:guid}/delete/cancel", CancelDeletionAsync));

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        OrganizationBody body,
        IOrganizations organizations,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await organizations
                .CreateAsync(
                    AccessContext.Of(browser.Required.Subject),
                    body.Name ?? string.Empty,
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            created => TypedResults.Json(
                CreatedOrganizationView.Of(created),
                OrganizationJson.Default.CreatedOrganizationView,
                contentType: null,
                StatusCodes.Status201Created));
    }

    private static async Task<IResult> RequestDeletionAsync(
        OrganizationReasonBody body,
        IOrganizations organizations,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await organizations
                .RequestDeletionAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new OrganizationId(id),
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> CancelDeletionAsync(
        OrganizationReasonBody body,
        IOrganizations organizations,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await organizations
                .CancelDeletionAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new OrganizationId(id),
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
