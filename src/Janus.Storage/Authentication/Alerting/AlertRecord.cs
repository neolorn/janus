using System;

namespace Janus.Storage.Authentication.Alerting;

/// <summary>
/// The <c>alerts</c> row: one condition, and when it last went out.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-002. One alert per sustained attack, never one per attempt:
/// without this an attacker triggers alerts to drain the prepaid balance.
/// </remarks>
internal sealed class AlertRecord
{
    /// <summary>The <c>key</c> column, which is this table key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The <c>at</c> column.</summary>
    public DateTimeOffset At { get; set; }
}
