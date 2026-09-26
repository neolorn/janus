using System;
using Janus.Authentication.Alerting;
using Janus.Core;

namespace Janus.Storage.Authentication.Alerting;

/// <summary>
/// The <c>raised_alerts</c> row: one condition waiting for the alert channels.
/// </summary>
/// <remarks>Implements OPS-ALERT-001 and CONV-DESIGN-002.</remarks>
internal sealed class RaisedAlertRecord
{
    /// <summary>The <c>id</c> column, which is this table key.</summary>
    public RaisedAlertId Id { get; set; }

    /// <summary>The <c>raised_at</c> column.</summary>
    public DateTimeOffset RaisedAt { get; set; }

    /// <summary>The <c>idempotency_key</c> column.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>The <c>condition</c> column.</summary>
    public AlertCondition Condition { get; set; }

    /// <summary>The <c>details</c> column, a JSON object.</summary>
    public string Details { get; set; } = string.Empty;
}
