using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Core;

namespace Janus.Authorization.Tests.Grants;

/// <summary>
/// The grants a deployment recorded as written or revoked, held in memory in the order
/// they were recorded.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that keeps what it is given, so a test reads back the entry a
/// grant left.
/// </remarks>
internal sealed class GrantAuditInMemory : IGrantAudit
{
    private readonly List<GrantChange> _changes = [];

    /// <summary>
    /// Every grant recorded, oldest first.
    /// </summary>
    public IReadOnlyList<GrantChange> Changes => _changes;

    /// <inheritdoc/>
    public ValueTask CreatedAsync(
        Grant created,
        SubjectId actor,
        string? breakGlassReason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(created);

        _changes.Add(new GrantChange(AuditActions.GrantCreated, created.Id, created.Subject, actor, created.Reason)
        {
            BreakGlassReason = breakGlassReason,
        });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CreatedAsync(Grant created, SystemPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(created);
        ArgumentNullException.ThrowIfNull(principal);

        _changes.Add(new GrantChange(AuditActions.GrantCreated, created.Id, created.Subject, null, created.Reason)
        {
            Principal = principal.Name,
        });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RevokedAsync(Grant revoked, string? breakGlassReason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revoked);

        _changes.Add(new GrantChange(
            AuditActions.GrantRevoked,
            revoked.Id,
            revoked.Subject,
            revoked.RevokedBy,
            revoked.RevocationReason ?? throw new ArgumentException("The grant is not revoked.", nameof(revoked)))
        {
            BreakGlassReason = breakGlassReason,
        });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RevokedAsync(
        Grant revoked,
        SystemPrincipal principal,
        SubjectId endSetBy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revoked);
        ArgumentNullException.ThrowIfNull(principal);

        _changes.Add(new GrantChange(
            AuditActions.GrantRevoked,
            revoked.Id,
            revoked.Subject,
            null,
            revoked.RevocationReason ?? throw new ArgumentException("The grant is not revoked.", nameof(revoked)))
        {
            Principal = principal.Name,
            EndSetBy = endSetBy,
        });

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// One grant as it was recorded.
    /// </summary>
    /// <param name="Action">Whether it was written or revoked.</param>
    /// <param name="Grant">Which grant.</param>
    /// <param name="Holder">Who holds it.</param>
    /// <param name="Actor">The person who acted, or nothing where a system principal did.</param>
    /// <param name="Reason">Why.</param>
    internal sealed record GrantChange(
        AuditAction Action,
        GrantId Grant,
        GrantSubject Holder,
        SubjectId? Actor,
        string Reason)
    {
        /// <summary>
        /// The system principal that acted, or nothing where a person did.
        /// </summary>
        public string? Principal { get; init; }

        /// <summary>
        /// The reason given at the use of the break-glass credential, where the grant
        /// was written or revoked in the session it opened, or nothing.
        /// </summary>
        public string? BreakGlassReason { get; init; }

        /// <summary>
        /// The administrator who set the end a system principal carried out, where one did.
        /// </summary>
        public SubjectId? EndSetBy { get; init; }
    }
}
