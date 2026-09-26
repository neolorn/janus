using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Janus.Core;

namespace Janus.Authorization.Tests.Roles;

/// <summary>
/// The changes to roles a deployment wrote down, held in memory in the order they
/// were made.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that keeps what it is given, so a test reads back the entry a
/// change left.
/// </remarks>
internal sealed class RoleAuditInMemory : IRoleAudit
{
    private readonly List<RoleChange> _changes = [];

    /// <summary>
    /// Every change recorded, oldest first.
    /// </summary>
    public IReadOnlyList<RoleChange> Changes => _changes;

    /// <summary>
    /// Every role a system principal created, oldest first.
    /// </summary>
    public List<(RoleName Role, IReadOnlyList<Permission> After, SystemPrincipal Principal)> Principals { get; } = [];

    /// <inheritdoc/>
    public ValueTask DefinedAsync(
        RoleName role,
        IReadOnlyList<Permission>? before,
        IReadOnlyList<Permission> after,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _changes.Add(new RoleChange(AuditActions.RoleDefined, role, before, after, reason, actor, at));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DefinedAsync(
        RoleName role,
        IReadOnlyList<Permission> after,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Principals.Add((role, after, principal));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemovedAsync(
        RoleName role,
        IReadOnlyList<Permission> before,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _changes.Add(new RoleChange(AuditActions.RoleRemoved, role, before, After: null, reason, actor, at));

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// One change as it was written down.
    /// </summary>
    /// <param name="Action">Whether the role was defined or removed.</param>
    /// <param name="Role">Which role.</param>
    /// <param name="Before">What it permitted, or nothing where it was new.</param>
    /// <param name="After">What it permits, or nothing where it was removed.</param>
    /// <param name="Reason">Why.</param>
    /// <param name="Actor">Who.</param>
    /// <param name="At">When.</param>
    internal sealed record RoleChange(
        AuditAction Action,
        RoleName Role,
        IReadOnlyList<Permission>? Before,
        IReadOnlyList<Permission>? After,
        string Reason,
        SubjectId Actor,
        DateTimeOffset At);
}
