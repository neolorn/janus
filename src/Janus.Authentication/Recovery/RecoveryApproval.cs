using System;
using Janus.Core;

namespace Janus.Authentication.Recovery;

/// <summary>
/// One approver standing behind one account's re-enrolment: who approved, for whom,
/// on which of the account's channels they confirmed the person, and when.
/// </summary>
/// <param name="Subject">Whose account is being recovered.</param>
/// <param name="Approver">Who approved it.</param>
/// <param name="Channel">The channel the confirmation was made on.</param>
/// <param name="At">When.</param>
/// <param name="SpentAt">
/// When the link it stood behind went out, and nothing while it is still standing:
/// a spent approval counts against the day's limits and toward no further link.
/// </param>
/// <remarks>
/// Implements AUTH-RECOV-002 and AUTH-RECOV-003. The written reason is not here: it
/// is about the person and lives under their own key in the audit trail.
/// </remarks>
internal sealed record RecoveryApproval(
    SubjectId Subject,
    SubjectId Approver,
    string Channel,
    DateTimeOffset At,
    DateTimeOffset? SpentAt = null);
