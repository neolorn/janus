using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Alerting;

/// <summary>
/// The hourly pass that raises this host's clock drifting beyond the tolerance a
/// time-based code is accepted within.
/// </summary>
/// <param name="reference">What the environment measured, where the deployment registered it.</param>
/// <param name="configuration">Where the tolerance is read.</param>
/// <param name="alerts">Where the drift goes.</param>
/// <param name="time">The clock the alert is stamped with.</param>
/// <remarks>
/// Implements INF-HOST-001 AC2, OPS-ALERT-001 and INF-BG-001. A code accepted a step
/// either side of the current one is accepted from a clock that far out and no further,
/// so the tolerance is <c>factor.totp.drift</c> steps; a clock beyond it rejects valid
/// codes silently. A drift the environment could not measure, or a deployment that
/// registered nothing to measure it, is raised as a degradation, because the clock is
/// then not known to be within it.
/// </remarks>
internal sealed class ClockDriftWatch(
    IClockReference? reference,
    IConfigurationStore configuration,
    IAlertChannels alerts,
    TimeProvider time)
{
    private const string Absent = "clock.reference.absent";
    private const string Unread = "clock.reference.unread";

    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>Nothing, or the failure where what was found could not be raised.</returns>
    public async ValueTask<Result> WatchAsync(CancellationToken cancellationToken)
    {
        if (reference is null)
        {
            return await alerts.RaiseAsync(Degraded(Absent), cancellationToken).ConfigureAwait(false);
        }

        Result<TimeSpan> measured = await reference.OffsetAsync(cancellationToken).ConfigureAwait(false);

        if (measured.Match(offset => (TimeSpan?)offset, _ => null) is not TimeSpan offset)
        {
            return await alerts.RaiseAsync(Degraded(Unread), cancellationToken).ConfigureAwait(false);
        }

        // An unreadable tolerance is the default's, so the pass still judges the clock.
        int steps = (await configuration
                .ReadAsync(Settings.FactorTotpDrift, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.FactorTotpDrift.Default);

        var tolerance = TimeSpan.FromSeconds((long)steps * TotpCodes.StepSeconds);

        if (offset.Duration() <= tolerance)
        {
            return Result.Success();
        }

        return await alerts
            .RaiseAsync(
                Alerts.Of(AlertCondition.ClockDrift, scope: null, time.GetUtcNow(), Drifted(offset, tolerance)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private AlertRaised Degraded(string scope) =>
        Alerts.Of(AlertCondition.Degradation, scope, time.GetUtcNow());

    private static Dictionary<string, JsonElement> Drifted(TimeSpan offset, TimeSpan tolerance) =>
        new(capacity: 2, StringComparer.Ordinal)
        {
            ["offsetSeconds"] = JsonSerializer.SerializeToElement(offset.TotalSeconds),
            ["toleranceSeconds"] = JsonSerializer.SerializeToElement(tolerance.TotalSeconds),
        };
}
