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
    public List<(DefinedRole After, SystemPrincipal Principal)> Principals { get; } = [];

    /// <inheritdoc/>
    public ValueTask DefinedAsync(
        DefinedRole? before,
        DefinedRole after,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(after);

        _changes.Add(new RoleChange(AuditActions.RoleDefined, after.Name, before, after, reason, actor, at)
        {
            BreakGlassReason = breakGlassReason,
        });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DefinedAsync(
        DefinedRole after,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Principals.Add((after, principal));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemovedAsync(
        DefinedRole before,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(before);

        _changes.Add(new RoleChange(AuditActions.RoleRemoved, before.Name, before, After: null, reason, actor, at)
        {
            BreakGlassReason = breakGlassReason,
        });

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// One change as it was written down.
    /// </summary>
    /// <param name="Action">Whether the role was defined or removed.</param>
    /// <param name="Role">Which role.</param>
    /// <param name="Before">The role as it stood, or nothing where it was new.</param>
    /// <param name="After">The role as it stands, or nothing where it was removed.</param>
    /// <param name="Reason">Why.</param>
    /// <param name="Actor">Who.</param>
    /// <param name="At">When.</param>
    internal sealed record RoleChange(
        AuditAction Action,
        RoleName Role,
        DefinedRole? Before,
        DefinedRole? After,
        string Reason,
        SubjectId Actor,
        DateTimeOffset At)
    {
        /// <summary>
        /// The reason given at the use of the break-glass credential, where the change
        /// was made in the session it opened, or nothing.
        /// </summary>
        public string? BreakGlassReason { get; init; }
    }
}
