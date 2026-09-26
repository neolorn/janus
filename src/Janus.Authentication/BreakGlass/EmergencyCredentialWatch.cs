using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;

namespace Janus.Authentication.BreakGlass;

/// <summary>
/// The hourly pass that raises the absence of a break-glass credential until one is
/// generated.
/// </summary>
/// <param name="store">Where the issues are kept.</param>
/// <param name="alerts">Where the absence goes.</param>
/// <param name="time">The clock the alert is stamped with.</param>
/// <remarks>
/// Implements OPS-BOOT-001 AC3, OPS-ALERT-001 and INF-BG-001. Nothing dismisses the
/// alert: it is raised at every pass while no issue stands, whether none was ever
/// generated or the last one was spent or replaced by nothing, and it stops only when
/// one is generated. OPS-ALERT-002 keeps it to one alert inside its window.
/// </remarks>
internal sealed class EmergencyCredentialWatch(
    IBreakGlassStore store,
    IAlertChannels alerts,
    TimeProvider time)
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>Nothing, or the failure where the absence could not be raised.</returns>
    public async ValueTask<Result> WatchAsync(CancellationToken cancellationToken)
    {
        if (await store.StandingAsync(cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Success();
        }

        return await alerts
            .RaiseAsync(Alerts.Of(AlertCondition.NoEmergencyCredential, scope: null, time.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
    }
}
