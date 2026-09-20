using System;
using Janus.Core;

namespace Janus.Hosting.Recovery;

/// <summary>
/// What an opened enrolment session tells the browser. The identifier is not in it:
/// the browser carries the first-contact cookie, and the session is found from that.
/// </summary>
/// <param name="ExpiresAt">When it ends.</param>
/// <param name="MailboxLost">
/// Whether a new address may be confirmed by the new address alone (AUTH-RECOV-002).
/// </param>
/// <remarks>Implements AUTH-RECOV-002, D-147 and D-148.</remarks>
internal sealed record EnrolmentSessionView(DateTimeOffset ExpiresAt, bool MailboxLost)
{
    /// <summary>
    /// Reads a session.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public static EnrolmentSessionView Of(EnrolmentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new EnrolmentSessionView(session.ExpiresAt, session.MailboxLost);
    }
}
