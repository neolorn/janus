using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Hosting.Sessions;

/// <summary>
/// The local IP-to-city database, which no deployment holds yet.
/// </summary>
/// <param name="events">Where a raised condition is published.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements INT-GEN-006. The file and the job that refreshes it are that item's own
/// work; until they exist there is no database to read, which is the condition the
/// item already describes: no location is shown and the degradation is raised. Nothing
/// here calls out to a third party, then or later.
/// </remarks>
internal sealed class LocationDatabase(IEvents events, TimeProvider time) : ILocationResolver
{
    private const string Absent = "location.database.absent";

    /// <inheritdoc/>
    public async ValueTask<SessionLocation?> ResolveAsync(
        string address,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        // What is degraded is the deployment and not the request, so the condition is
        // raised under the absent file and the router carries one alert a window
        // rather than one a sign-in (OPS-ALERT-002). The session is recorded without
        // a location either way (INT-GEN-006).
        await events
            .PublishAsync(
                Alerts.Of(AlertCondition.Degradation, Absent, time.GetUtcNow()),
                cancellationToken)
            .ConfigureAwait(false);

        return null;
    }
}
