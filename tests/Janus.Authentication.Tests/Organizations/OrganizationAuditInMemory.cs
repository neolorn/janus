using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Core;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// The changes to organizations a deployment wrote down, held in memory in the order
/// they were made.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that keeps what it is given, so a test reads back the entry a
/// change left.
/// </remarks>
internal sealed class OrganizationAuditInMemory : IOrganizationAudit
{
    private readonly List<OrganizationChange> _changes = [];

    /// <summary>
    /// Every change recorded, oldest first.
    /// </summary>
    public IReadOnlyList<OrganizationChange> Changes => _changes;

    /// <inheritdoc/>
    public ValueTask RecordedAsync(
        AuditAction action,
        OrganizationId organization,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _changes.Add(new OrganizationChange(action, organization, reason, actor, at));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DomainChangedAsync(
        AuditAction action,
        OrganizationId organization,
        string domain,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _changes.Add(new OrganizationChange(action, organization, reason, actor, at) { Domain = domain });

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// One change as it was written down.
    /// </summary>
    /// <param name="Action">What changed.</param>
    /// <param name="Organization">Which organization.</param>
    /// <param name="Reason">Why.</param>
    /// <param name="Actor">Who.</param>
    /// <param name="At">When.</param>
    internal sealed record OrganizationChange(
        AuditAction Action,
        OrganizationId Organization,
        string Reason,
        SubjectId Actor,
        DateTimeOffset At)
    {
        /// <summary>
        /// Which domain of the lock changed, where one did.
        /// </summary>
        public string? Domain { get; init; }
    }
}
