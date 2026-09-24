using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What an account may do with the addresses and numbers it is reached at: add one,
/// prove it, move the primary role, settle where security notices go, give one up and
/// take it back, and, where the deployment holds one of a kind, change it in place.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, REG-IDENT-002 and REG-IDENT-004 to REG-IDENT-007. Adding
/// and removing are step-up actions and answer the step-up code where the session has
/// not proved enough; setting the primary and the backup setting are neither. The undo
/// and the abandon are link-borne: a hostile change leaves the account no session that
/// could reach them, so each takes the token its message carried and nothing else.
/// </remarks>
public interface IIdentifiers
{
    /// <summary>
    /// Adds an email or a phone, unverified, and sends a code and a link to it. The
    /// answer is the same whether or not the value already belongs to an account: a
    /// value that does receives nothing and its holder is told instead.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="kind">Whether it is an email or a phone.</param>
    /// <param name="value">The value as the person entered it.</param>
    /// <param name="source">
    /// The address the request came from, which the source restrictions count the
    /// message against.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> AddAsync(
        AccessContext context,
        SessionId session,
        IdentifierKind kind,
        string value,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Proves an identifier with the code its message carried.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="identifier">Which identifier.</param>
    /// <param name="code">The code typed in.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> VerifyAsync(
        AccessContext context,
        IdentifierId identifier,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// What a verification link does where it was opened. A press from the browser
    /// that staged the change proves it; anything else changes nothing and yields the
    /// code to type.
    /// </summary>
    /// <param name="session">
    /// The session the requesting browser carries, or nothing where it carries none.
    /// </param>
    /// <param name="linkToken">The token the message carried.</param>
    /// <param name="press">Whether the person pressed the control.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the press proved it, and where it did not, what the landing shows.
    /// </returns>
    ValueTask<Result<LinkLanding>> LandAsync(
        SessionId? session,
        [NeverLogged] string linkToken,
        bool press,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends the verification an add or a replace is waiting on, which is the control
    /// the link offers where it was opened somewhere else. It answers the same whether
    /// or not the token resolves to anything.
    /// </summary>
    /// <param name="linkToken">The token the message carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success.</returns>
    ValueTask<Result> AbandonAsync([NeverLogged] string linkToken, CancellationToken cancellationToken);

    /// <summary>
    /// Makes a verified identifier the primary of its kind, which needs neither
    /// step-up nor confirmation.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="identifier">Which identifier.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> MakePrimaryAsync(
        AccessContext context,
        IdentifierId identifier,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Settles what a kind's security-notice set holds beyond the primary. The set as
    /// it stood before the change is what is notified of it.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="kind">The kind the setting governs.</param>
    /// <param name="choice">What it adds to the primary.</param>
    /// <param name="named">The identifier it names, where it names one.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> SetBackupAsync(
        AccessContext context,
        IdentifierKind kind,
        BackupChoice choice,
        IdentifierId? named,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes an identifier, at once. The remaining security-notice set receives an
    /// undo link; the removed identifier receives a notice with no link and no powers
    /// and never serves this account again.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request arrived on, which survives it.</param>
    /// <param name="identifier">Which identifier.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> RemoveAsync(
        AccessContext context,
        SessionId session,
        IdentifierId identifier,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Puts a removed identifier back from the undo link its message carried.
    /// </summary>
    /// <param name="linkToken">The token the undo link carried.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> UndoAsync(
        [NeverLogged] string linkToken,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stages the value that will take the place of one the account holds, where the
    /// deployment holds one identifier of that kind. The swap applies when the new
    /// value proves, and where the account has no other channel at all, when the
    /// displaced one confirms as well.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="identifier">Which identifier is being changed.</param>
    /// <param name="value">The new value as the person entered it.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> ReplaceAsync(
        AccessContext context,
        SessionId session,
        IdentifierId identifier,
        string value,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stages the same replacement from an enrolment session opened for an account
    /// whose mailbox the approver recorded as lost. The approver's confirmation on a
    /// recorded channel stands in for the displaced address, so the new value proves
    /// alone and the displaced one is not asked.
    /// </summary>
    /// <param name="enrolment">The enrolment session the browser opened.</param>
    /// <param name="identifier">Which identifier is being changed.</param>
    /// <param name="value">The new value as the person entered it.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> ReplaceAsync(
        EnrolmentSessionId enrolment,
        IdentifierId identifier,
        string value,
        string source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Proves a value staged from an enrolment session by the code sent to it.
    /// </summary>
    /// <param name="enrolment">The enrolment session the browser opened.</param>
    /// <param name="identifier">Which identifier the code was sent for.</param>
    /// <param name="code">The code as the person typed it.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> VerifyAsync(
        EnrolmentSessionId enrolment,
        IdentifierId identifier,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken);
}
