using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;
using Janus.Hosting.Accounts;

namespace Janus.Hosting.Recovery;

/// <summary>
/// The document the enrolment screen renders from, which <c>GET /enrol</c> answers and
/// the opening of the session carries. The session's identifier is not in it: the
/// browser carries the first-contact cookie, and the session is found from that.
/// </summary>
/// <param name="Username">The account's primary email, FE-PM-002's hidden field.</param>
/// <param name="Password">Whether a password is set, and when it was last set.</param>
/// <param name="Offers">The credentials the session may set.</param>
/// <param name="MailboxLost">
/// Whether a new address may be confirmed by the new address alone (AUTH-RECOV-002).
/// </param>
/// <param name="Identifiers">The emails a replace names, where the mailbox was lost.</param>
/// <param name="ExpiresAt">When it ends.</param>
/// <remarks>Implements AUTH-RECOV-002, FE-SEC-001, D-147, D-148 and D-224.</remarks>
internal sealed record EnrolmentSessionView(
    string Username,
    PasswordView Password,
    IReadOnlyList<Factor> Offers,
    bool MailboxLost,
    IReadOnlyList<EnrolmentIdentifierView> Identifiers,
    DateTimeOffset ExpiresAt)
{
    /// <summary>
    /// Reads a document.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The document is absent.</exception>
    public static EnrolmentSessionView Of(EnrolmentDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new EnrolmentSessionView(
            document.Username,
            PasswordView.Of(document.Password),
            document.Offers,
            document.MailboxLost,
            [.. document.Identifiers.Select(EnrolmentIdentifierView.Of)],
            document.ExpiresAt);
    }
}
