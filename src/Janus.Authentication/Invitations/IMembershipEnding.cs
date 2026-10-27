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
    /// The account's current membership of the organization, read without ending it.
    /// </summary>
    /// <param name="subject">Whose membership.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The membership, or nothing where the account holds no current membership of the
    /// organization.
    /// </returns>
    ValueTask<MembershipId?> FindAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken);

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

    /// <summary>
    /// The instant the account's current membership of the organization is set to end at,
    /// read without changing it.
    /// </summary>
    /// <param name="subject">Whose membership.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The instant, or nothing where no end is set or the account holds no current
    /// membership of the organization.
    /// </returns>
    ValueTask<DateTimeOffset?> EndSetAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets the account's current membership of the organization to end at a later
    /// instant, or moves the instant set (IDN-MEM-001, D-213).
    /// </summary>
    /// <param name="subject">Whose membership.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="endsAt">The instant it ends.</param>
    /// <param name="setBy">The administrator setting it.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The membership, or nothing where the account holds no current membership of the
    /// organization.
    /// </returns>
    ValueTask<MembershipId?> SetEndAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset endsAt,
        SubjectId setBy,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels the end the account's current membership of the organization is set to
    /// reach (IDN-MEM-001, D-213).
    /// </summary>
    /// <param name="subject">Whose membership.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The membership, or nothing where no end is set or the account holds no current
    /// membership of the organization.
    /// </returns>
    ValueTask<MembershipId?> CancelEndAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken);
}
