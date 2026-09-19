using Janus.Authentication.Alerting;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Alerting;

/// <summary>
/// Where a channel that carried an alert nowhere is written down.
/// </summary>
/// <param name="log">The host's logger.</param>
/// <remarks>
/// Implements OPS-ALERT-003 and CONV-LOG-001. The condition and the channel are the
/// whole entry: no destination reaches it (CONV-LOG-003).
/// </remarks>
internal sealed partial class AlertLog(ILogger<AlertLog> log) : IAlertLog
{
    /// <inheritdoc/>
    public void Unreachable(string condition, string channel) => Unreachable(log, condition, channel);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "The alert {Condition} reached nobody by {Channel}.")]
    private static partial void Unreachable(ILogger log, string condition, string channel);
}
