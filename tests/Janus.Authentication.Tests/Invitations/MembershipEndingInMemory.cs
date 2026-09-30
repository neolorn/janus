using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authentication.Tests.Policies;
using Janus.Core;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// The memberships administrators ended, held in memory.
/// </summary>
/// <param name="lookup">Where the membership is taken out of, so that the policy no longer resolves through it.</param>
/// <remarks>
/// CONV-TEST-004: a fake that ends only a membership the account holds, as the rows
/// do, and keeps what it ended.
/// </remarks>
internal sealed class MembershipEndingInMemory(MembershipLookupInMemory lookup) : IMembershipEnding
{
    /// <summary>
    /// Every membership ended, oldest first.
    /// </summary>
    public List<EndedMembership> Ended { get; } = [];

    /// <inheritdoc/>
    public async ValueTask<MembershipId?> FindAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await lookup.OfAsync(subject, cancellationToken).ConfigureAwait(false)).Contains(organization)
            ? new MembershipId(Guid.CreateVersion7())
            : null;

    /// <inheritdoc/>
    public ValueTask<MembershipId?> EndAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (!lookup.Leave(subject, organization))
        {
            return ValueTask.FromResult<MembershipId?>(null);
        }

        var membership = new MembershipId(Guid.CreateVersion7(at));

        Ended.Add(new EndedMembership(membership, subject, organization, at));

        return ValueTask.FromResult<MembershipId?>(membership);
    }
}
