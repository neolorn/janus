using System;
using Janus.Authentication.Mailboxes;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Mailboxes;

/// <summary>
/// Where what the mail server threw at a push is kept: in the log, as every other fault
/// the library catches is.
/// </summary>
/// <param name="log">The host's logger.</param>
/// <remarks>
/// Implements BFF-ERR-002 AC2, INT-MAIL-007, CONV-LOG-001 and CONV-LOG-003. The entry is
/// the fault's type and frames, never its message.
/// </remarks>
internal sealed partial class MailServerFaults(ILogger<MailServerFaults> log) : IMailServerFaults
{
    /// <inheritdoc/>
    public void Keep(Exception fault) => Faulted(log, FaultLog.Of(fault));

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "A push to the mail server faulted and is a failed attempt: {Fault}.")]
    private static partial void Faulted(ILogger log, string fault);
}
