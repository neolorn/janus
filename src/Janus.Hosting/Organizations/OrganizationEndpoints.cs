using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Organizations;

/// <summary>
/// The organization administration of chapter 09 section 8a: creating an
/// organization, requesting its deletion, cancelling the request, reading and
/// replacing its policy, the domains it locks its members to, the invitations into its
/// membership, and the end of a membership.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-002, IDN-ORG-003, IDN-ORG-004, IDN-ORG-006, REG-DOM-001,
/// IDN-LIFE-009a, IDN-MEM-001, REG-INV-001, REG-MAIL-003, AUTH-STEP-002a, LIB-API-005,
/// CONV-CODE-006 and CONV-DESIGN-006.
/// Each is one line to <see cref="IOrganizations"/>, <see cref="IOrganizationDomains"/>
/// or <see cref="IInvitations"/>, which judge the permission, the step-up and what the
/// reason says; a body missing a member it requires is refused before any is called.
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

        _ = SessionRequired.On(endpoints.MapPost("/admin/organizations", CreateAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.IdentifierMixedScript));
        _ = SessionRequired.On(endpoints.MapPost("/admin/organizations/{id}/delete", RequestDeletionAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.OrganizationNotFound, ErrorCodes.StepUpRequired,
                    ErrorCodes.OrganizationProtected)
                .Binding<OrganizationId>("id"));
        _ = SessionRequired.On(endpoints.MapPost("/admin/organizations/{id}/delete/cancel", CancelDeletionAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.OrganizationNotFound, ErrorCodes.StepUpRequired,
                    ErrorCodes.OrganizationProtected, ErrorCodes.DeletionWindowElapsed)
                .Binding<OrganizationId>("id"));
        _ = SessionRequired.On(endpoints.MapGet("/admin/organizations/{id}/policy", PolicyAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.OrganizationNotFound)
                .Binding<OrganizationId>("id"));
        _ = SessionRequired.On(endpoints.MapPut("/admin/organizations/{id}/policy", ReplacePolicyAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.OrganizationNotFound, ErrorCodes.StepUpRequired,
                    ErrorCodes.ConfigurationChangeReasonRequired,
                    ErrorCodes.ConfigurationPolicyBelowSystem, ErrorCodes.ConfigurationValueBelowFloor,
                    ErrorCodes.ConfigurationValueNotAllowed)
                .Binding<OrganizationId>("id"));
        _ = SessionRequired.On(endpoints.MapGet("/admin/organizations/{id}/domains", DomainsAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.OrganizationNotFound)
                .Binding<OrganizationId>("id"));
        _ = SessionRequired.On(endpoints.MapPost("/admin/organizations/{id}/domains", AddDomainAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.OrganizationNotFound, ErrorCodes.StepUpRequired,
                    ErrorCodes.ConfigurationChangeReasonRequired,
                    ErrorCodes.ConfigurationValueNotAllowed)
                .Binding<OrganizationId>("id"));
        _ = SessionRequired.On(endpoints.MapPost(
            "/admin/organizations/{id}/domains/{domain}/verify",
            VerifyDomainAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.OrganizationNotFound, ErrorCodes.StepUpRequired,
                    ErrorCodes.DomainNotFound, ErrorCodes.DomainUnverified,
                    ErrorCodes.ConfigurationChangeReasonRequired)
                .Binding<OrganizationId>("id"));
        _ = SessionRequired.On(endpoints.MapDelete(
            "/admin/organizations/{id}/domains/{domain}",
            RemoveDomainAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.OrganizationNotFound, ErrorCodes.StepUpRequired,
                    ErrorCodes.ConfigurationChangeReasonRequired)
                .Binding<OrganizationId>("id"));
        _ = SessionRequired.On(endpoints.MapPost("/admin/organizations/{id}/invitations", InviteAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.OrganizationNotFound, ErrorCodes.StepUpRequired,
                    ErrorCodes.InvitationMailboxHeld, ErrorCodes.MailboxTaken,
                    ErrorCodes.InvitationAddressRequired, ErrorCodes.IdentifierDomainNotAllowed,
                    ErrorCodes.IdentifierMixedScript, ErrorCodes.IdentifierInvalid,
                    ErrorCodes.GrantUnresolved, ErrorCodes.RequestInvalid,
                    ErrorCodes.RestrictionExceeded)
                .Binding<OrganizationId>("id"));
        _ = SessionRequired.On(endpoints.MapDelete(
            "/admin/organizations/{id}/invitations/{invitationId}",
            RevokeInvitationAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.Restricted, ErrorCodes.OrganizationNotFound,
                    ErrorCodes.InvitationNotFound, ErrorCodes.InvitationExpired)
                .Binding<OrganizationId>("id")
                .Binding<InvitationId>("invitationId"));
        _ = SessionRequired.On(endpoints.MapDelete(
            "/admin/organizations/{id}/memberships/{subject}",
            EndMembershipAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.Restricted, ErrorCodes.OrganizationNotFound,
                    ErrorCodes.StepUpRequired, ErrorCodes.MembershipNotFound)
                .Binding<OrganizationId>("id")
                .Binding<SubjectId>("subject"));

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
            await organizations
                .CreateAsync(
                    browser.Asking,
                    name,
                    reason,
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
        OrganizationId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(browser);

        if (body.Reason?.Trim() is not { Length: > 0 and <= 1024 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await organizations
                .RequestDeletionAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> CancelDeletionAsync(
        OrganizationReasonBody body,
        IOrganizations organizations,
        RequestSession browser,
        OrganizationId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(browser);

        if (body.Reason?.Trim() is not { Length: > 0 and <= 1024 } reason)
        {
            return Answers.Malformed("reason");
        }

        return Answers.Of(
            await organizations
                .CancelDeletionAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> PolicyAsync(
        IOrganizations organizations,
        RequestSession browser,
        OrganizationId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await organizations
                .PolicyAsync(browser.Asking, id, cancellationToken)
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
        OrganizationId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organizations);
        ArgumentNullException.ThrowIfNull(browser);

        Error? failure = null;
        (PolicyOverride replacement, string reason) = OrganizationPolicyBody
            .Read(body, id)
            .Match(read => read, error => Withheld<(PolicyOverride, string)>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        if (Unexplained(id, reason) is IResult unexplained)
        {
            return unexplained;
        }

        return Answers.Of(
            await organizations
                .ReplacePolicyAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    replacement,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> DomainsAsync(
        IOrganizationDomains domains,
        RequestSession browser,
        OrganizationId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await domains
                .DomainsAsync(browser.Asking, id, cancellationToken)
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
        OrganizationId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(browser);

        if (body.Domain is not { Length: > 0 } domain)
        {
            return Answers.Malformed("domain");
        }

        if (Unexplained(id, body.Reason) is IResult unexplained)
        {
            return unexplained;
        }

        return Answers.Of(
            await domains
                .AddDomainAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    domain,
                    body.Reason!,
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
        OrganizationId id,
        string domain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(browser);

        if (Unexplained(id, body.Reason) is IResult unexplained)
        {
            return unexplained;
        }

        return Answers.Of(
            await domains
                .VerifyDomainAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    domain,
                    body.Reason!,
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
        OrganizationId id,
        string domain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(browser);

        if (Unexplained(id, body.Reason) is IResult unexplained)
        {
            return unexplained;
        }

        return Answers.Of(
            await domains
                .RemoveDomainAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    domain,
                    body.Reason!,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> InviteAsync(
        InvitationBody body,
        IInvitations invitations,
        RequestSession browser,
        HttpContext context,
        OrganizationId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(invitations);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        (InvitationRequest? request, string member) = body.Read();

        if (request is null)
        {
            return Answers.Malformed(member);
        }

        return Answers.Of(
            await invitations
                .IssueAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    request,
                    RequestOrigin.Source(context.Request),
                    cancellationToken)
                .ConfigureAwait(false),
            issued => TypedResults.Json(
                IssuedInvitationView.Of(issued),
                OrganizationJson.Default.IssuedInvitationView,
                contentType: null,
                StatusCodes.Status201Created));
    }

    private static async Task<IResult> RevokeInvitationAsync(
        IInvitations invitations,
        RequestSession browser,
        OrganizationId id,
        InvitationId invitationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitations);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await invitations
                .RevokeAsync(
                    browser.Asking,
                    id,
                    invitationId,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> EndMembershipAsync(
        IInvitations invitations,
        RequestSession browser,
        HttpContext context,
        OrganizationId id,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitations);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        return Answers.Of(
            await invitations
                .EndMembershipAsync(
                    browser.Asking,
                    browser.Required.Id,
                    id,
                    subject,
                    RequestOrigin.Source(context.Request),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    // 09 section 8a: a change of an organization's policy or of its domains is a
    // configuration change of its policy key, so a reason absent or blank is refused
    // with the code a change without one is, naming that key, before any permission is
    // asked; one past the bound of API-CONV-002 is a request the boundary does not read.
    private static IResult? Unexplained(OrganizationId organization, string? reason) =>
        (reason?.Trim().Length ?? 0) switch
        {
            0 => Answers.Refused(Error.From(
                ErrorCodes.ConfigurationChangeReasonRequired,
                "key",
                JsonSerializer.SerializeToElement(Settings.OrganizationPolicy.For(organization.ToString()).ToString()))),
            > 1024 => Answers.Malformed("reason"),
            _ => null,
        };

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
