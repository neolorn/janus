using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Core;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// The grants of a deployment held in memory, with the per-account counter the cached
/// inputs are keyed by.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that answers from the rows it holds, so a test that writes a
/// grant sees it the way the store would show it.
/// </remarks>
internal sealed class GrantsInMemory : IGrantStore
{
    private readonly Dictionary<GrantId, Grant> _grants = [];
    private readonly Dictionary<SubjectId, long> _versions = [];

    /// <summary>
    /// How many times a counter has been read.
    /// </summary>
    public int Reads { get; private set; }

    /// <summary>
    /// Raises the counter of one account, as a grant or membership change does.
    /// </summary>
    /// <param name="subject">Whose counter.</param>
    public void Bump(SubjectId subject) =>
        _versions[subject] = _versions.GetValueOrDefault(subject) + 1;

    /// <inheritdoc/>
    public ValueTask<Grant?> FindAsync(GrantId id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_grants.GetValueOrDefault(id));

    /// <inheritdoc/>
    public ValueTask CreateAsync(Grant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        _grants[grant.Id] = grant;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(Grant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        _grants[grant.Id] = grant;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> ExistsAsync(Grant grant, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return ValueTask.FromResult(_grants.Values.Any(held =>
            held.Subject == grant.Subject
            && held.Role == grant.Role
            && held.Organization == grant.Organization
            && held.ResourceType == grant.ResourceType
            && held.ResourceId == grant.ResourceId
            && held.Deny == grant.Deny
            && held.IsLive(at)));
    }

    /// <inheritdoc/>
    public ValueTask<bool> NamesAsync(RoleName role, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_grants.Values.Any(grant => grant.Role == role));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Grant>> HeldByAsync(
        IReadOnlyList<GrantSubject> holders,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holders);

        return ValueTask.FromResult<IReadOnlyList<Grant>>(
        [
            .. _grants.Values.Where(grant =>
                holders.Contains(grant.Subject)
                && grant.Organization == organization
                && grant.IsLive(at)),
        ]);
    }

    /// <inheritdoc/>
    public ValueTask<long> VersionAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        Reads++;

        return ValueTask.FromResult(_versions.GetValueOrDefault(subject));
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Grant>> MaterialisedAsync(
        RoleName role,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);

        return ValueTask.FromResult<IReadOnlyList<Grant>>(
        [
            .. _grants.Values.Where(grant =>
                grant.Kind == GrantKind.Materialised
                && grant.Role == role
                && grant.Organization == organization
                && grant.IsLive(at)
                && grant.ResourceType == type
                && grant.ResourceId is ResourceId record
                && resources.Contains(record)),
        ]);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Grant>> OnAsync(
        ResourceReference reference,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Grant>>(
        [
            .. _grants.Values.Where(grant =>
                grant.Organization == organization
                && grant.IsLive(at)
                && (grant.IsOrganizationWide
                    || (grant.ResourceType == reference.Type && grant.ResourceId == reference.Id))),
        ]);
}
