using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Groups;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authorization.Groups;

/// <summary>
/// What a change to a group leaves in the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTHZ-GROUP-001, OPS-CFG-007 and IDN-AUD-001. The record carries the
/// group, its name, the member where one changed, and why, in the organization the
/// group belongs to.
/// </remarks>
internal sealed class GroupAudit(IAuditStore records, TimeProvider time) : IGroupAudit
{
    private static readonly AuditAction Created = AuditActions.GroupCreated;

    private static readonly AuditAction Removed = AuditActions.GroupRemoved;

    private static readonly AuditAction MemberAdded = AuditActions.GroupMemberAdded;

    private static readonly AuditAction MemberRemoved = AuditActions.GroupMemberRemoved;

    /// <inheritdoc/>
    public async ValueTask CreatedAsync(
        Group group,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(Created, group, member: null, reason, actor, at, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask RemovedAsync(
        Group group,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(Removed, group, member: null, reason, actor, at, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask MemberAddedAsync(
        Group group,
        GrantSubject member,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(MemberAdded, group, member, reason, actor, at, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask MemberRemovedAsync(
        Group group,
        GrantSubject member,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(MemberRemoved, group, member, reason, actor, at, cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask AppendAsync(
        AuditAction action,
        Group group,
        GrantSubject? member,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        var details = new Dictionary<string, JsonElement>(capacity: 5, StringComparer.Ordinal)
        {
            ["group"] = JsonSerializer.SerializeToElement(group.Id.ToString()),
            ["name"] = JsonSerializer.SerializeToElement(group.Name),
            ["reason"] = JsonSerializer.SerializeToElement(reason),
        };

        if (member is GrantSubject joined)
        {
            details["memberType"] = JsonSerializer.SerializeToElement(
                joined.Type is SubjectType.User ? "user" : "group");
            details["memberId"] = JsonSerializer.SerializeToElement(joined.Value.ToString());
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
                    group.Organization,
                    details),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
