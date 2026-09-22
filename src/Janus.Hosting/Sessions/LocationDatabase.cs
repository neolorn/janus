using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Hosting.Sessions;

/// <summary>
/// The local IP-to-city database, which no deployment holds yet.
/// </summary>
/// <param name="alerts">Where a raised condition goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements INT-GEN-006. The file and the job that refreshes it are that item's own
/// work; until they exist there is no database to read, which is the condition the
/// item already describes: no location is shown and the degradation is raised. Nothing
/// here calls out to a third party, then or later.
/// </remarks>
internal sealed class LocationDatabase(AlertRouter alerts, TimeProvider time) : ILocationResolver
{
    private const string Reason = "location.database.absent";

    /// <inheritdoc/>
    public async ValueTask<SessionLocation?> ResolveAsync(
        string address,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        DateTimeOffset now = time.GetUtcNow();

        // One alert a deduplication window, not one a sign-in: the router keeps that,
        // and what is degraded is the deployment and not the request (OPS-ALERT-002).
        // An alert that cannot be delivered is no reason to refuse the session, which
        // is recorded without a location either way (INT-GEN-006).
        _ = (await alerts
                .RaiseAsync(
                    new AlertRaised(
                        now,
                        Alerts.Key(AlertCondition.Degradation, Reason),
                        AlertCondition.Degradation,
                        Alerts.Severity(AlertCondition.Degradation),
                        new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                        {
                            ["reason"] = JsonSerializer.SerializeToElement(Reason),
                        }),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(delivered => delivered.Deduplicated, _ => false);

        return null;
    }
}
