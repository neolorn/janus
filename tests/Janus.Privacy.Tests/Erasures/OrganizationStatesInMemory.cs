using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;

namespace Janus.Privacy.Tests.Erasures;

/// <summary>
/// The organization deletion windows, in a dictionary, with what each erasure ended.
/// </summary>
internal sealed class OrganizationStatesInMemory : IOrganizationStates
{
    private readonly Dictionary<OrganizationId, PendingOrganizationDeletion> _deleting = [];
    private readonly Dictionary<OrganizationId, int> _members = [];

    /// <summary>
    /// The organizations erased, in the order the pass reached them.
    /// </summary>
    public List<OrganizationId> Erased { get; } = [];

    /// <summary>
    /// Puts an organization in its deletion window, as a deployment has one.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="since">When the window began.</param>
    /// <param name="members">How many current memberships it has.</param>
    public void Deletes(OrganizationId organization, DateTimeOffset since, int members = 0)
    {
        _deleting[organization] = new PendingOrganizationDeletion(organization, since);
        _members[organization] = members;
    }

    /// <summary>
    /// Cancels a window, as a deployment does inside it.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    public void Cancels(OrganizationId organization) => _deleting.Remove(organization);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<PendingOrganizationDeletion>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<PendingOrganizationDeletion>>(
        [
            .. _deleting.Values
                .Where(deletion => deletion.Since <= before && !Erased.Contains(deletion.Organization))
                .OrderBy(deletion => deletion.Since),
        ]);

    /// <inheritdoc/>
    public ValueTask<int> EraseAsync(
        OrganizationId organization,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        if (!_deleting.TryGetValue(organization, out PendingOrganizationDeletion? deletion))
        {
            throw new InvalidOperationException("The organization is not being deleted.");
        }

        if (at < deletion.Since + window)
        {
            throw new InvalidOperationException("The grace window has not elapsed.");
        }

        Erased.Add(organization);

        return ValueTask.FromResult(_members[organization]);
    }
}
