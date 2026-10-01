using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Recovery;

/// <summary>
/// The enrolment session an admin-assisted link opened, as the operations it reaches
/// read it.
/// </summary>
/// <param name="links">Where the link the session stands on is held.</param>
/// <param name="work">The transaction ending one writes inside.</param>
/// <param name="time">The clock the lifetime is measured on.</param>
/// <remarks>
/// Implements AUTH-RECOV-002 and D-147. The session is the spent link and nothing
/// else, so it is capped by that link's own lifetime and has none of its own; ending
/// it is removing the link, which is what makes a completed enrolment unrepeatable
/// (D-148).
/// </remarks>
internal sealed class EnrolmentSessions(
    IRecoveryLinkStore links,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// The session a browser carries, where it is still open.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session, or nothing where none answers to it.</returns>
    public async ValueTask<EnrolmentSession?> FindAsync(
        EnrolmentSessionId session,
        CancellationToken cancellationToken)
    {
        RecoveryLink? link = await links.FindAsync(session, cancellationToken).ConfigureAwait(false);

        return link is null || link.HasExpired(time.GetUtcNow())
            ? null
            : new EnrolmentSession(session, link.Subject, link.ExpiresAt, link.MailboxLost);
    }

    /// <summary>
    /// Whether a session is still open, read under a lock on the link it stands on and
    /// held until the caller's transaction ends, so a second completion of it waits for
    /// the first to end it and finds it ended (D-148, CONV-DESIGN-003).
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it is open.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<bool> HoldAsync(
        EnrolmentSessionId session,
        CancellationToken cancellationToken) =>
        await links.FindForUpdateAsync(session, cancellationToken).ConfigureAwait(false)
            is RecoveryLink link
            && !link.HasExpired(time.GetUtcNow());

    /// <summary>
    /// Ends one, which completing the enrolment does: what the person set is used by
    /// signing in with it (D-148).
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of ending it.</returns>
    public async ValueTask EndAsync(EnrolmentSessionId session, CancellationToken cancellationToken)
    {
        RecoveryLink? link = await links.FindAsync(session, cancellationToken).ConfigureAwait(false);

        if (link is null)
        {
            return;
        }

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        await links.RemoveAsync(link.Fingerprint, cancellationToken).ConfigureAwait(false);
        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
    }
}
