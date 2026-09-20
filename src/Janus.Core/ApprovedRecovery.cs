using System;

namespace Janus.Core;

/// <summary>
/// What an approved re-enrolment tells the approver: when the link they caused to be
/// sent stops working. The link itself goes to the channel and never to the answer.
/// </summary>
/// <param name="EnrolmentLinkExpiresAt">
/// When the link stops working, and nothing where the approval was recorded and the
/// deployment requires more approvers than have yet stood behind it
/// (<c>recovery.approvers.required</c>): that is the one case in which an approval
/// sends no link.
/// </param>
/// <remarks>Implements AUTH-RECOV-002 and AUTH-RECOV-003.</remarks>
public sealed record ApprovedRecovery(DateTimeOffset? EnrolmentLinkExpiresAt);
