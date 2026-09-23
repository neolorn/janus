using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The way back into an account: the password a person resets themselves, the
/// re-enrolment an approver opens after confirming the person out of band, and the
/// report that a credential is gone.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTH-RECOV-002, AUTH-RECOV-002a, AUTH-RECOV-003,
/// AUTH-RECOV-005 and AUTH-RECOV-007. Recovery reaches the password and never a
/// second factor: what it may do about a lost factor is report it, and the notified
/// window is what stands between a stolen mailbox and an account.
/// </remarks>
public interface IRecovery
{
    /// <summary>
    /// Asks for a recovery link for an identifier. Succeeds whether or not an account
    /// holds it (AUTH-ABUSE-003); what differs is what arrives at the channel.
    /// </summary>
    /// <param name="identifier">The email or phone as it was entered.</param>
    /// <param name="language">
    /// The locale of the request, which the message goes out in where the account
    /// holds no language of its own (IDN-ATTR-001).
    /// </param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the delay the source has earned.</returns>
    ValueTask<Result> BeginAsync(
        string identifier,
        string language,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Spends a recovery link and sets the account's password. Removes no enrolled
    /// factor, and restores an account its holder deactivated (IDN-LIFE-013).
    /// </summary>
    /// <param name="token">The token the message carried.</param>
    /// <param name="password">The password, cleared by the caller after the call.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>auth.recovery.tokeninvalid</c>, <c>auth.recovery.tokenexpired</c>
    /// or what the password screening refused.
    /// </returns>
    ValueTask<Result> CompleteAsync(
        string token,
        string password,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Approves a re-enrolment for another account after confirming the person on a
    /// channel the account already holds, and sends the enrolment link to that
    /// channel.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request arrived on, which the gate reads.</param>
    /// <param name="subject">Whose account is being recovered.</param>
    /// <param name="reason">The written reason, which is recorded.</param>
    /// <param name="channelUsed">
    /// The channel the confirmation was made on, which SHALL be one the account holds.
    /// </param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// When the link stops working, or <c>auth.recovery.selfapproval</c>,
    /// <c>auth.recovery.reasonrequired</c>, <c>auth.recovery.channelnotonaccount</c>,
    /// <c>auth.stepup.required</c>, <c>authz.denied</c> or <c>auth.throttled</c>.
    /// </returns>
    ValueTask<Result<ApprovedRecovery>> ApproveAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        string reason,
        string channelUsed,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Spends an enrolment link and opens the enrolment session it stands for. The
    /// only endpoint that consumes that link (D-147).
    /// </summary>
    /// <param name="token">The token the message carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The session, or <c>auth.enrolment.tokeninvalid</c> or
    /// <c>auth.recovery.tokenexpired</c>.
    /// </returns>
    ValueTask<Result<EnrolmentSession>> BeginEnrolmentAsync(
        string token,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reports a credential lost, which suspends it at once and notifies every
    /// recorded channel. Asks no step-up: the person reporting has lost the factor a
    /// gate would ask for.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="credential">Which credential is gone.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// When the window ends, or <c>auth.lossreport.notpermitted</c>,
    /// <c>auth.lossreport.pending</c> or <c>auth.credential.notfound</c>.
    /// </returns>
    ValueTask<Result<LossReported>> ReportLossAsync(
        AccessContext context,
        AuthenticatorId credential,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a pending loss report, from a session of the account or from the link
    /// every notification carried, and returns the credential to active.
    /// </summary>
    /// <param name="context">Who is asking, which is nobody where a link is presented.</param>
    /// <param name="credential">Which credential.</param>
    /// <param name="cancelToken">The token the notification carried, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or <c>auth.credential.notfound</c> where no report answers.</returns>
    ValueTask<Result> CancelLossAsync(
        AccessContext? context,
        AuthenticatorId credential,
        string? cancelToken,
        CancellationToken cancellationToken);
}
