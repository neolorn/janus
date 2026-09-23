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
/// What a change to an organization's lifecycle, to its domain lock or to the
/// invitations into it leaves in the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-ORG-003, REG-DOM-001, IDN-LIFE-009a, IDN-AUD-001 and CONV-DESIGN-003.
/// The record is filed under the organization and carries the reason, the domain where
/// one changed, or the invitation where one was issued or revoked; the actor is both
/// identities.
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
}
