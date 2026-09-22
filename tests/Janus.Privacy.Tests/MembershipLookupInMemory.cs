using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Policies;

namespace Janus.Privacy.Tests;

/// <summary>
/// The memberships, holding what a test put there and nothing else.
/// </summary>
internal sealed class MembershipLookupInMemory : IMembershipLookup
{
    private readonly Dictionary<SubjectId, List<OrganizationId>> _memberships = [];

    /// <summary>
    /// Puts a principal in an organization.
    /// </summary>
    /// <param name="subject">The principal.</param>
    /// <param name="organization">The organization.</param>
    public void Add(SubjectId subject, OrganizationId organization)
    {
        if (!_memberships.TryGetValue(subject, out List<OrganizationId>? organizations))
        {
            organizations = [];
            _memberships[subject] = organizations;
        }

        organizations.Add(organization);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<OrganizationId>> OfAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<OrganizationId>>(
            _memberships.TryGetValue(subject, out List<OrganizationId>? organizations)
                ? organizations
                : []);
}
