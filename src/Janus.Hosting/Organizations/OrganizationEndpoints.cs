using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Organizations;

/// <summary>
/// The organization administration of chapter 09 section 8a: creating an
/// organization, requesting its deletion, cancelling the request, reading and
/// replacing its policy, and the domains it locks its members to.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-002, IDN-ORG-003, IDN-ORG-004, IDN-ORG-006, REG-DOM-001,
/// AUTH-STEP-002a, LIB-API-005 and CONV-DESIGN-006.
/// Each is one line to <see cref="IOrganizations"/> or <see cref="IOrganizationDomains"/>,
/// which judge the permission, the step-up and the reason.
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
        _ = SessionRequired.On(endpoints.MapGet("/admin/organizations/{id:guid}/policy", PolicyAsync));
        _ = SessionRequired.On(endpoints.MapPut("/admin/organizations/{id:guid}/policy", ReplacePolicyAsync));
        _ = SessionRequired.On(endpoints.MapGet("/admin/organizations/{id:guid}/domains", DomainsAsync));
        _ = SessionRequired.On(endpoints.MapPost("/admin/organizations/{id:guid}/domains", AddDomainAsync));
        _ = SessionRequired.On(endpoints.MapPost(
            "/admin/organizations/{id:guid}/domains/{domain}/verify",
            VerifyDomainAsync));
        _ = SessionRequired.On(endpoints.MapDelete(
            "/admin/organizations/{id:guid}/domains/{domain}",
            RemoveDomainAsync));

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

    private static async Task<IResult> PolicyAsync(
        IOrganizations organizations,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await organizations
                .PolicyAsync(AccessContext.Of(browser.Required.Subject), new OrganizationId(id), cancellationToken)
                .ConfigureAwait(false),
            policy => TypedResults.Json(
                OrganizationPolicyView.Of(policy),
                OrganizationJson.Default.OrganizationPolicyView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> ReplacePolicyAsync(
        JsonElement body,
        IOrganizations organizations,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(browser);

        var organization = new OrganizationId(id);
        Error? failure = null;
        (PolicyOverride replacement, string reason) = OrganizationPolicyBody
            .Read(body, organization)
            .Match(read => read, error => Withheld<(PolicyOverride, string)>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        return Answers.Of(
            await organizations
                .ReplacePolicyAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    organization,
                    replacement,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> DomainsAsync(
        IOrganizationDomains domains,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await domains
                .DomainsAsync(AccessContext.Of(browser.Required.Subject), new OrganizationId(id), cancellationToken)
                .ConfigureAwait(false),
            held => TypedResults.Json<IReadOnlyList<OrganizationDomainView>>(
                [.. held.Select(OrganizationDomainView.Of)],
                OrganizationJson.Default.IReadOnlyListOrganizationDomainView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    // The response names the record that proves the domain, which is what the
    // administrator publishes before asking for it to be verified.
    private static async Task<IResult> AddDomainAsync(
        OrganizationDomainBody body,
        IOrganizationDomains domains,
        RequestSession browser,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await domains
                .AddDomainAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new OrganizationId(id),
                    body.Domain ?? string.Empty,
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            added => TypedResults.Json(
                OrganizationDomainView.Of(added),
                OrganizationJson.Default.OrganizationDomainView,
                contentType: null,
                StatusCodes.Status201Created));
    }

    private static async Task<IResult> VerifyDomainAsync(
        OrganizationReasonBody body,
        IOrganizationDomains domains,
        RequestSession browser,
        Guid id,
        string domain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await domains
                .VerifyDomainAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new OrganizationId(id),
                    domain ?? string.Empty,
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            verified => TypedResults.Json(
                OrganizationDomainView.Of(verified),
                OrganizationJson.Default.OrganizationDomainView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    // A removal carries its reason, which is free text and goes in the body rather
    // than in an address a log keeps.
    private static async Task<IResult> RemoveDomainAsync(
        [FromBody] OrganizationReasonBody body,
        IOrganizationDomains domains,
        RequestSession browser,
        Guid id,
        string domain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await domains
                .RemoveDomainAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new OrganizationId(id),
                    domain ?? string.Empty,
                    body.Reason ?? string.Empty,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
