using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authorization.Grants;

/// <summary>
/// What a grant written or revoked leaves in the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-003, AUTHZ-INHERIT-002 and IDN-AUD-001 (D-197, D-203, D-206).
/// The record names who acted, the grant's holder as its subject where the holder is an
/// account and none where it is a group, under the grant's organization, with the grant,
/// its role and the reason (chapter 10 section 5.24). A revocation names the revoker the
/// grant carries, the nil subject for a removal.
/// </remarks>
internal sealed class GrantAudit(IAuditStore records, TimeProvider time) : IGrantAudit
{
    /// <inheritdoc/>
    public async ValueTask CreatedAsync(
        Grant created,
        SubjectId actor,
        string? breakGlassReason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(created);

        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    AuditActions.GrantCreated,
                    created.GrantedAt,
                    actor,
                    Holder(created),
                    breakGlassReason,
                    created.Organization,
                    Details(created, created.Reason)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CreatedAsync(Grant created, SystemPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(created);
        ArgumentNullException.ThrowIfNull(principal);

        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    AuditActions.GrantCreated,
                    created.GrantedAt,
                    principal,
                    Holder(created),
                    created.Organization,
                    Details(created, created.Reason)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The grant is not revoked.</exception>
    public async ValueTask RevokedAsync(Grant revoked, string? breakGlassReason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revoked);

        if (revoked is not { RevokedBy: SubjectId revoker, RevokedAt: DateTimeOffset at, RevocationReason: string reason })
        {
            throw new ArgumentException("Only a revoked grant is recorded as revoked.", nameof(revoked));
        }

        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    AuditActions.GrantRevoked,
                    at,
                    revoker,
                    Holder(revoked),
                    breakGlassReason,
                    revoked.Organization,
                    Details(revoked, reason)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // Chapter 10 section 5.24 (D-206): a group is no data subject, so a grant it holds
    // concerns no account.
    private static SubjectId? Holder(Grant grant) =>
        grant.Subject.Type == SubjectType.User ? new SubjectId(grant.Subject.Value) : null;

    private static Dictionary<string, JsonElement> Details(Grant grant, string reason) =>
        new(capacity: 3, StringComparer.Ordinal)
        {
            ["grant"] = JsonSerializer.SerializeToElement(grant.Id.ToString()),
            ["role"] = JsonSerializer.SerializeToElement(grant.Role.ToString()),
            ["reason"] = JsonSerializer.SerializeToElement(reason),
        };
}
