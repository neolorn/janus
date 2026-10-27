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
/// Implements AUTHZ-GRANT-004, AUTHZ-GRANT-005, OPS-CFG-007, IDN-AUD-001 and
/// IDN-PRIN-001. The record carries the role, what it permitted and handed out before,
/// what it permits and hands out after, and why; a role is the deployment's, so no
/// organization is recorded. A role a system principal created is recorded under the
/// principal, and the reason it states is the record's reason.
/// </remarks>
internal sealed class RoleAudit(IAuditStore records, TimeProvider time) : IRoleAudit
{
    private static readonly AuditAction Defined = AuditActions.RoleDefined;

    private static readonly AuditAction Removed = AuditActions.RoleRemoved;

    /// <inheritdoc/>
    public async ValueTask DefinedAsync(
        DefinedRole? before,
        DefinedRole after,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(after);

        await AppendAsync(Defined, after.Name, before, after, reason, actor, breakGlassReason, at, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DefinedAsync(
        DefinedRole after,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(principal);

        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    Defined,
                    at,
                    principal,
                    subject: null,
                    organization: null,
                    Details(after.Name, before: null, after, principal.Reason)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RemovedAsync(
        DefinedRole before,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(before);

        await AppendAsync(Removed, before.Name, before, after: null, reason, actor, breakGlassReason, at, cancellationToken)
            .ConfigureAwait(false);
    }

    // 10 section 5.24 (D-212): each side is the role's permissions and the roles it
    // hands out, written in the order the role's reading answers them.
    private static JsonElement Written(DefinedRole? role) =>
        role is null
            ? JsonSerializer.SerializeToElement<object?>(null)
            : JsonSerializer.SerializeToElement(new Dictionary<string, string[]>(capacity: 2, StringComparer.Ordinal)
            {
                ["permissions"] = [.. role.Permissions.Select(permission => permission.ToString())],
                ["handsOut"] = [.. role.HandsOut.Select(handed => handed.ToString())],
            });

    private static Dictionary<string, JsonElement> Details(
        RoleName role,
        DefinedRole? before,
        DefinedRole? after,
        string reason) =>
        new(capacity: 4, StringComparer.Ordinal)
        {
            ["role"] = JsonSerializer.SerializeToElement(role.ToString()),
            ["before"] = Written(before),
            ["after"] = Written(after),
            ["reason"] = JsonSerializer.SerializeToElement(reason),
        };

    private async ValueTask AppendAsync(
        AuditAction action,
        RoleName role,
        DefinedRole? before,
        DefinedRole? after,
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
                    organization: null,
                    Details(role, before, after, reason)),
                cancellationToken)
            .ConfigureAwait(false);
}
