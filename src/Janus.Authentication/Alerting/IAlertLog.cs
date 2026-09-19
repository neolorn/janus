namespace Janus.Authentication.Alerting;

/// <summary>
/// Where a channel that could not carry an alert is written down. Nothing here
/// carries a destination: the condition and the channel are the whole record
/// (CONV-LOG-003).
/// </summary>
/// <remarks>Implements OPS-ALERT-003 and OPS-OBS-002.</remarks>
internal interface IAlertLog
{
    /// <summary>
    /// Records that a channel which was to carry an alert carried it nowhere.
    /// </summary>
    /// <param name="condition">The condition identifier.</param>
    /// <param name="channel">The channel that carried nothing.</param>
    void Unreachable(string condition, string channel);
}
