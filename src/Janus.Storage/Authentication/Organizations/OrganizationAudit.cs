using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
                    effectiveSubject: null,
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
        SubjectId actor,
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
                    organization,
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["invitation"] = JsonSerializer.SerializeToElement(invitation.Value),
                    }),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask MembershipEndedAsync(
        OrganizationId organization,
        MembershipId membership,
        SubjectId member,
        SubjectId actor,
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
                    organization,
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["membership"] = JsonSerializer.SerializeToElement(membership.Value),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
