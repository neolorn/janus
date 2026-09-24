using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Core;
using Janus.Identity.Organizations;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// The membership an administrator ends, over the <c>memberships</c> table.
/// </summary>
/// <param name="memberships">Where the account's memberships are read and the ended one recorded.</param>
/// <remarks>
/// Implements IDN-MEM-001 and CONV-LAYOUT-001. The end is the membership's own
/// transition, so the row stays and carries when it ended.
/// </remarks>
internal sealed class MembershipEnding(IMembershipStore memberships) : IMembershipEnding
{
    /// <inheritdoc/>
    public async ValueTask<MembershipId?> EndAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Membership> held = await memberships
            .FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (held.SingleOrDefault(membership => membership.IsCurrent && membership.Organization == organization)
            is not Membership current)
        {
            return null;
        }

        current.End(at);

        await memberships.RecordAsync(current, cancellationToken).ConfigureAwait(false);

        return current.Id;
    }
}
