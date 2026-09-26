using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Recovery;

/// <summary>
/// Where the approvals standing behind an account's re-enrolment are held, which is
/// what makes an approver count above one possible and what both rate limits count.
/// </summary>
/// <remarks>Implements AUTH-RECOV-002 and CONV-DESIGN-003.</remarks>
internal interface IRecoveryApprovalStore
{
    /// <summary>
    /// Records one approval.
    /// </summary>
    /// <param name="approval">The approval.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(RecoveryApproval approval, CancellationToken cancellationToken);

    /// <summary>
    /// The approvals still standing behind one account since an instant, which is the
    /// set the required approver count is read from.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="from">The earliest counted.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The approvals.</returns>
    ValueTask<IReadOnlyList<RecoveryApproval>> StandingForAsync(
        SubjectId subject,
        DateTimeOffset from,
        CancellationToken cancellationToken);

    /// <summary>
    /// When each approval one account has drawn after an instant was given, which is
    /// what the account's limit counts and what says when it admits another: one given
    /// at the instant itself has left the window, so the window's end is exactly when
    /// the earliest counted stops counting.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="from">The instant the window opens after.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The instants, earliest first.</returns>
    ValueTask<IReadOnlyList<DateTimeOffset>> ForAsync(
        SubjectId subject,
        DateTimeOffset from,
        CancellationToken cancellationToken);

    /// <summary>
    /// When each approval one approver has given after an instant was given, which is
    /// what the approver's limit counts and what says when it admits another, the
    /// instant itself outside the window as it is for an account.
    /// </summary>
    /// <param name="approver">Who approved.</param>
    /// <param name="from">The instant the window opens after.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The instants, earliest first.</returns>
    ValueTask<IReadOnlyList<DateTimeOffset>> ByAsync(
        SubjectId approver,
        DateTimeOffset from,
        CancellationToken cancellationToken);

    /// <summary>
    /// Spends what stood behind one account, which issuing the link does: the next
    /// re-enrolment is approved again from nothing, and what was spent still counts
    /// against the day's limits.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of spending them.</returns>
    ValueTask SpendAsync(SubjectId subject, DateTimeOffset at, CancellationToken cancellationToken);
}
