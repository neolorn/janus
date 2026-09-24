using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// Where the membership an administrator ends is closed.
/// </summary>
/// <remarks>
/// Implements IDN-MEM-001 and CONV-LAYOUT-001. The membership is the identity area's;
/// this is what ending one asks of it, inside the transaction the end runs in, so
/// nothing here commits.
/// </remarks>
internal interface IMembershipEnding
{
    /// <summary>
    /// Ends the account's current membership of the organization. The account and the
    /// organization persist, and the record stays with its end.
    /// </summary>
    /// <param name="subject">Whose membership.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="at">When it ends.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The membership that ended, or nothing where the account holds no current
    /// membership of the organization.
    /// </returns>
    ValueTask<MembershipId?> EndAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
