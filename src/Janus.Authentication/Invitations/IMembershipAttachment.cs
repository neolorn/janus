using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// Where the membership an acknowledged invitation attaches is written, with the roles
/// it grants.
/// </summary>
/// <remarks>
/// Implements REG-INV-001, IDN-LIFE-009a, IDN-MEM-002 and CONV-LAYOUT-001. The
/// membership is the identity area's and the grants are the authorization area's; this
/// is what an acknowledgement asks of them, inside the transaction it runs in, so
/// nothing here commits.
/// </remarks>
internal interface IMembershipAttachment
{
    /// <summary>
    /// Attaches a membership carrying what the person acknowledged, and grants each
    /// role across the organization. A grant the account already holds is not written
    /// again.
    /// </summary>
    /// <param name="subject">Whose membership.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="acknowledged">The documents the person acknowledged, at the versions shown.</param>
    /// <param name="roles">The roles granted across the organization.</param>
    /// <param name="grantedBy">Who granted them, which is who issued the invitation.</param>
    /// <param name="reason">Why they were granted.</param>
    /// <param name="multiple">What <c>organization.multiplememberships</c> allows.</param>
    /// <param name="at">When the person acknowledged.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The membership, or the refusal: <c>identity.membership.limitreached</c> where the
    /// account may hold no further membership, or holds one of the organization already.
    /// </returns>
    ValueTask<Result<MembershipId>> AttachAsync(
        SubjectId subject,
        OrganizationId organization,
        IReadOnlyList<InvitationDocument> acknowledged,
        IReadOnlyList<RoleName> roles,
        SubjectId grantedBy,
        string reason,
        bool multiple,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
