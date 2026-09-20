using System.Collections.Generic;
using Janus.Authentication.Alerting;

namespace Janus.Authentication.Tests.Alerting;

/// <summary>
/// Where an unreachable channel is written down, holding the entries so a test can
/// read the residual case of a gateway account at zero.
/// </summary>
internal sealed class AlertLogInMemory : IAlertLog
{
    /// <summary>
    /// Every channel recorded as having carried nothing, in order.
    /// </summary>
    public List<(string Condition, string Channel)> Unreached { get; } = [];

    /// <inheritdoc/>
    public void Unreachable(string condition, string channel) => Unreached.Add((condition, channel));
}
