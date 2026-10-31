using System;

namespace Janus.Authentication.Mailboxes;

/// <summary>
/// Where what the mail server threw at a push is written down: by its type and frames,
/// as every other fault the library catches is, and never by its message, which can
/// carry a value (CONV-LOG-003).
/// </summary>
/// <remarks>
/// Implements BFF-ERR-002 AC2 and INT-MAIL-007. The call is a port because the entry a
/// fault is kept as, and the structured log call itself, are the host layer's.
/// </remarks>
internal interface IMailServerFaults
{
    /// <summary>
    /// Keeps a fault the mail server threw at a push, which the push counts as a failed
    /// attempt.
    /// </summary>
    /// <param name="fault">What it threw.</param>
    void Keep(Exception fault);
}
