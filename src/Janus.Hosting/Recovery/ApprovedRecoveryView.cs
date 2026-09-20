using System;
using Janus.Core;

namespace Janus.Hosting.Recovery;

/// <summary>
/// What an approval tells the approver. The link itself went to the channel.
/// </summary>
/// <param name="EnrolmentLinkExpiresAt">
/// When the link stops working, and nothing where more approvers are required than
/// have yet stood behind the account.
/// </param>
/// <remarks>Implements AUTH-RECOV-002 and AUTH-RECOV-003.</remarks>
internal sealed record ApprovedRecoveryView(DateTimeOffset? EnrolmentLinkExpiresAt)
{
    /// <summary>
    /// Reads an approval.
    /// </summary>
    /// <param name="approved">What the approval left behind.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The approval is absent.</exception>
    public static ApprovedRecoveryView Of(ApprovedRecovery approved)
    {
        ArgumentNullException.ThrowIfNull(approved);

        return new ApprovedRecoveryView(approved.EnrolmentLinkExpiresAt);
    }
}
