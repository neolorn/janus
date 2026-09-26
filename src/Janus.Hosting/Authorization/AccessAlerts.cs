using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// Where a condition the gate observes goes.
/// </summary>
/// <param name="alerts">Where the raised condition goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-ALERT-001, AUTHZ-GATE-004 and CONV-DESIGN-003. The severity of a
/// condition and what it deduplicates under are the alert table's, so the gate names
/// the condition and this carries it to the one router every condition goes through.
/// </remarks>
internal sealed class AccessAlerts(IAlertChannels alerts, TimeProvider time) : IAccessAlerts
{
    /// <inheritdoc/>
    public async ValueTask RaiseAsync(
        AlertCondition condition,
        string? scope,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken) =>
        await alerts
            .RaiseAsync(Alerts.Of(condition, scope, time.GetUtcNow(), details), cancellationToken)
            .ConfigureAwait(false);
}
