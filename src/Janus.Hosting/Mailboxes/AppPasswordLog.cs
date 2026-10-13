using Janus.Authentication.Mailboxes;
using Janus.Core;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Mailboxes;

/// <summary>
/// Where an app password left at the mail server is written down.
/// </summary>
/// <param name="log">The host's logger.</param>
/// <remarks>
/// Implements INT-MAIL-010 and CONV-LOG-001. The holder and the code are the whole
/// entry: neither the secret nor the label reaches it (CONV-LOG-003).
/// </remarks>
internal sealed partial class AppPasswordLog(ILogger<AppPasswordLog> log) : IAppPasswordLog
{
    /// <inheritdoc/>
    public void RevocationFailed(SubjectId holder, ErrorCode failure) => Unrevoked(log, holder, failure);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "An app password of {Holder} whose creation was refused was not revoked at the mail server: {Code}.")]
    private static partial void Unrevoked(ILogger log, SubjectId holder, ErrorCode code);
}
