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
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(Created, group, member: null, reason, actor, breakGlassReason, at, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask RemovedAsync(
        Group group,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(Removed, group, member: null, reason, actor, breakGlassReason, at, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask MemberAddedAsync(
        Group group,
        GrantSubject member,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(MemberAdded, group, member, reason, actor, breakGlassReason, at, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask MemberRemovedAsync(
        Group group,
        GrantSubject member,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(MemberRemoved, group, member, reason, actor, breakGlassReason, at, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask MemberRemovedAsync(
        Group group,
        GrantSubject member,
        string reason,
        SystemPrincipal principal,
        SubjectId endSetBy,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(principal);

        Dictionary<string, JsonElement> details = Details(group, member, reason);

        // D-217: the job's record names the administrator who set the end it carried out,
        // and the account it took out as its subject.
        details["endSetBy"] = JsonSerializer.SerializeToElement(endSetBy.ToString());

        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    MemberRemoved,
                    at,
                    principal,
                    member.Type is SubjectType.User ? new SubjectId(member.Value) : null,
                    group.Organization,
                    details),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Dictionary<string, JsonElement> Details(Group group, GrantSubject? member, string reason)
    {
        var details = new Dictionary<string, JsonElement>(capacity: 6, StringComparer.Ordinal)
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

        return details;
    }

    private async ValueTask AppendAsync(
        AuditAction action,
        Group group,
        GrantSubject? member,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        Dictionary<string, JsonElement> details = Details(group, member, reason);

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
                    group.Organization,
                    details),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
