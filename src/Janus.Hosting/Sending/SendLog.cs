using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Sending;

/// <summary>
/// What the publisher records about a send's immediate attempt that faulted.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003, CONV-ERR-003 and CONV-LOG-001. The fault is kept as its
/// fault log entry, so no message and nothing of the send reaches the log
/// (CONV-LOG-003).
/// </remarks>
internal static partial class SendLog
{
    /// <summary>
    /// The attempt that follows a send's commit faulted, and the message is left to the
    /// publisher's next pass.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="fault">The fault log entry of what was thrown.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "The immediate attempt of a send faulted and is left to the publisher: {Fault}.")]
    public static partial void AttemptLeft(ILogger log, string fault);
}
