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
/// What a revocation no person made leaves in the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-003, AUTHZ-INHERIT-002 and IDN-AUD-001 (D-203). The record names
/// the revoker the grant carries, the nil subject for a removal, under the grant's
/// organization, with the grant, its role and the reason (chapter 10 section 5.24).
/// </remarks>
internal sealed class GrantAudit(IAuditStore records, TimeProvider time) : IGrantAudit
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The grant is not revoked.</exception>
    public async ValueTask RevokedAsync(Grant revoked, CancellationToken cancellationToken)
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
                    subject: null,
                    breakGlassReason: null,
                    revoked.Organization,
                    new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
                    {
                        ["grant"] = JsonSerializer.SerializeToElement(revoked.Id.ToString()),
                        ["role"] = JsonSerializer.SerializeToElement(revoked.Role.ToString()),
                        ["reason"] = JsonSerializer.SerializeToElement(reason),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
