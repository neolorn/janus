using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The minor takedown: the trigger that stops everything at once, the reading of what
/// the hosts have confirmed since, and the reversal for an adult misjudged.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-003, PRIV-MINOR-002 and chapter 09 section 8a. The
/// takedown borrows the deletion timer and nothing else: the subject is sent no notice
/// and holds no link, and the only way back is the reversal inside the window.
/// </remarks>
public interface ITakedowns
{
    /// <summary>
    /// Phase one, in one transaction: the account passes through suspension into its
    /// grace window, every session of it ends, the trigger and the reason are recorded,
    /// and the delivery the hosts confirm against is written.
    /// </summary>
    /// <param name="context">Who is triggering it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="trigger">What raised the indication.</param>
    /// <param name="reason">Why, in the words of the person triggering it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The takedown and when its erasure runs, or the refusal:
    /// <c>identity.takedown.active</c> where one is already running on the account.
    /// </returns>
    ValueTask<Result<ExecutedTakedown>> ExecuteAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        TakedownTrigger trigger,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// The latest takedown of an account and how far the hosts have got with it.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The progress, or the refusal: <c>identity.takedown.notfound</c> where the account
    /// was never taken down.
    /// </returns>
    ValueTask<Result<TakedownProgress>> ReadAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reverses a takedown inside its window and restores the account to active. What
    /// the hosts undid at the trigger is not restored.
    /// </summary>
    /// <param name="context">Who is reversing it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="reason">Why, in the words of the person reversing it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.takedown.windowelapsed</c> once the window
    /// has closed.
    /// </returns>
    ValueTask<Result> ReverseAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        string reason,
        CancellationToken cancellationToken);
}
