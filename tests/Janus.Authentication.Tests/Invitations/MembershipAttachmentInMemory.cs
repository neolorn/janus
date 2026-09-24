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
/// The memberships acknowledgements attached, held in memory with the grants they
/// carried.
/// </summary>
/// <param name="lookup">Where a membership is placed so that the policy resolves through it.</param>
/// <remarks>
/// CONV-TEST-004: a fake that keeps what it is given and refuses what the membership
/// refuses, so a second membership where the setting allows one fails here as it fails
/// against the rows (IDN-MEM-002).
/// </remarks>
internal sealed class MembershipAttachmentInMemory(MembershipLookupInMemory lookup) : IMembershipAttachment
{
    /// <summary>
    /// Every membership an acknowledgement attached, oldest first.
    /// </summary>
    public List<AttachedMembership> Attached { get; } = [];

    /// <inheritdoc/>
    public async ValueTask<Result<MembershipId>> AttachAsync(
        SubjectId subject,
        OrganizationId organization,
        IReadOnlyList<InvitationDocument> acknowledged,
        IReadOnlyList<RoleName> roles,
        SubjectId grantedBy,
        string reason,
        bool multiple,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationId> held = await lookup.OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (held.Contains(organization) || (!multiple && held.Count > 0))
        {
            return Result.Failure<MembershipId>(Error.From(ErrorCodes.MembershipLimitReached));
        }

        var membership = new MembershipId(Guid.CreateVersion7(at));

        lookup.Place(subject, organization);
        Attached.Add(new AttachedMembership(
            membership,
            subject,
            organization,
            acknowledged,
            [.. roles.Distinct()],
            grantedBy,
            reason,
            at));

        return Result.Success(membership);
    }
}
