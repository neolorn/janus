using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// What a change to a role leaves in the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-004, OPS-CFG-007 and IDN-AUD-001. The record carries the
/// role, what it permitted before, what it permits after and why; a role is the
/// deployment's, so no organization is recorded.
/// </remarks>
internal sealed class RoleAudit(IAuditStore records, TimeProvider time) : IRoleAudit
{
    private static readonly AuditAction Defined = AuditActions.RoleDefined;

    private static readonly AuditAction Removed = AuditActions.RoleRemoved;

    /// <inheritdoc/>
    public async ValueTask DefinedAsync(
        RoleName role,
        IReadOnlyList<Permission>? before,
        IReadOnlyList<Permission> after,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(Defined, role, before, after, reason, actor, at, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask RemovedAsync(
        RoleName role,
        IReadOnlyList<Permission> before,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(Removed, role, before, after: null, reason, actor, at, cancellationToken)
            .ConfigureAwait(false);

    private static JsonElement Written(IReadOnlyList<Permission>? permissions) =>
        JsonSerializer.SerializeToElement(permissions?.Select(permission => permission.ToString()).ToArray());

    private async ValueTask AppendAsync(
        AuditAction action,
        RoleName role,
        IReadOnlyList<Permission>? before,
        IReadOnlyList<Permission>? after,
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
                    organization: null,
                    new Dictionary<string, JsonElement>(capacity: 4, StringComparer.Ordinal)
                    {
                        ["role"] = JsonSerializer.SerializeToElement(role.ToString()),
                        ["before"] = Written(before),
                        ["after"] = Written(after),
                        ["reason"] = JsonSerializer.SerializeToElement(reason),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
