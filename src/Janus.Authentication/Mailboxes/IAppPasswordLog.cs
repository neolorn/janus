using Janus.Core;

namespace Janus.Authentication.Mailboxes;

/// <summary>
/// Where an app password left at the mail server is written down: one whose creation
/// the gate refused when it was asked again, and whose revocation the server did not
/// take. Nothing here carries the secret or the label: the holder and the code the
/// server answered are the whole record (CONV-LOG-003, CONV-LOG-004).
/// </summary>
/// <remarks>
/// Implements INT-MAIL-010 and AUTHZ-GATE-006. The call is a port because the
/// structured log call itself is the host layer's, which is the one place the logging
/// abstraction exists.
/// </remarks>
internal interface IAppPasswordLog
{
    /// <summary>
    /// Records that an app password whose creation was refused stays at the server.
    /// </summary>
    /// <param name="holder">Whose app password it is.</param>
    /// <param name="failure">The code the revocation was answered with.</param>
    void RevocationFailed(SubjectId holder, ErrorCode failure);
}
