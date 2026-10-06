using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Invitations;
using Janus.Authentication.Organizations;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Organizations;

/// <summary>
/// What a change to an organization's lifecycle, to its domain lock, to the invitations
/// into it or to its memberships leaves in the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-ORG-003, REG-DOM-001, IDN-LIFE-009a, IDN-MEM-001, IDN-AUD-001,
/// IDN-PRIN-001 and CONV-DESIGN-003. The record is filed under the organization and
/// carries the reason, the domain where one changed, the invitation where one was
/// issued or revoked, or the membership where one ended; the actor is both identities,
/// except that a membership ended is done to its member, and a system principal is
/// recorded with its own reason and no subject.
/// </remarks>
internal sealed class OrganizationAudit(IAuditStore records, TimeProvider time) : IOrganizationAudit
{
    /// <inheritdoc/>
    public async ValueTask RecordedAsync(
        AuditAction action,
        OrganizationId organization,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    action,
                    at,
                    actor,
                    actor,
                    breakGlassReason,
                    organization,
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["reason"] = JsonSerializer.SerializeToElement(reason),
                    }),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask RecordedAsync(
        AuditAction action,
        OrganizationId organization,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    action,
                    at,
                    principal,
                    subject: null,
                    organization),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask DomainChangedAsync(
        AuditAction action,
        OrganizationId organization,
        string domain,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    action,
                    at,
                    actor,
                    actor,
                    breakGlassReason,
                    organization,
                    new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["domain"] = JsonSerializer.SerializeToElement(domain),
                        ["reason"] = JsonSerializer.SerializeToElement(reason),
                    }),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask InvitationChangedAsync(
        AuditAction action,
        OrganizationId organization,
        InvitationId invitation,
        MailboxTakeover? takeover,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
        {
            ["invitation"] = JsonSerializer.SerializeToElement(invitation.Value),
        };

        // Chapter 10 section 5.24: the former mailbox's choice and its reason, where the
        // invitation names one (REG-MAIL-003).
        if (takeover is not null)
        {
            details["formerMailbox"] = JsonSerializer.SerializeToElement(WrittenName.Of(takeover.Choice));
            details["reason"] = JsonSerializer.SerializeToElement(takeover.Reason);
        }

        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    action,
                    at,
                    actor,
                    actor,
                    breakGlassReason,
                    organization,
                    details),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask MembershipEndedAsync(
        OrganizationId organization,
        MembershipId membership,
        SubjectId member,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    AuditActions.MembershipEnded,
                    at,
                    actor,
                    member,
                    breakGlassReason,
                    organization,
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["membership"] = JsonSerializer.SerializeToElement(membership.Value),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
