using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Privacy;

namespace Janus.Hosting.Privacy;

/// <summary>
/// Where a condition the privacy area raises goes.
/// </summary>
/// <param name="events">Where the raised condition is published.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-ALERT-001, OPS-ALERT-002 and CONV-DESIGN-003. The severity of a
/// condition and what it deduplicates under are the alert table's, so the area names
/// the condition and this carries it to the one router every condition goes through.
/// </remarks>
internal sealed class PrivacyAlerts(IEvents events, TimeProvider time) : IPrivacyAlerts
{
    /// <inheritdoc/>
    public async ValueTask RaiseAsync(
        AlertCondition condition,
        string? scope,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken) =>
        await events
            .PublishAsync(Alerts.Of(condition, scope, time.GetUtcNow(), details), cancellationToken)
            .ConfigureAwait(false);
}
