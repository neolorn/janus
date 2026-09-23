using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Core;

namespace Janus.Authentication.Tests.Policies;

/// <summary>
/// Memberships held in memory, answering with nothing for a principal a test has not
/// placed in an organization.
/// </summary>
internal sealed class MembershipLookupInMemory : IMembershipLookup
{
    private readonly Dictionary<SubjectId, List<OrganizationId>> _held = [];

    /// <summary>
    /// Places a principal in an organization.
    /// </summary>
    /// <param name="subject">The principal.</param>
    /// <param name="organization">The organization.</param>
    public void Place(SubjectId subject, OrganizationId organization)
    {
        if (!_held.TryGetValue(subject, out List<OrganizationId>? organizations))
        {
            organizations = [];
            _held[subject] = organizations;
        }

        organizations.Add(organization);
    }

    /// <summary>
    /// Every principal placed in an organization.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <returns>Its members.</returns>
    public IReadOnlyList<SubjectId> Members(OrganizationId organization) =>
        [.. _held.Where(entry => entry.Value.Contains(organization)).Select(entry => entry.Key)];

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<OrganizationId>> OfAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<OrganizationId>>(
            _held.TryGetValue(subject, out List<OrganizationId>? organizations)
                ? organizations
                : []);
}
