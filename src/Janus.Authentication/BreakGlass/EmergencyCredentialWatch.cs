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
    /// <param name="context">The system principal the watch runs as.</param>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>Nothing, or the failure where the absence could not be raised.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may monitor.</exception>
    public async ValueTask<Result> WatchAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Monitoring(context);

        if (await store.StandingAsync(cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Success();
        }

        return await alerts
            .RaiseAsync(Alerts.Of(AlertCondition.NoEmergencyCredential, named: null, time.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
    }

    // INF-BG-002 AC1, IDN-PRIN-001 AC3 (D-166, 304): the watch runs as a named
    // principal that may monitor, and never as nobody.
    private static SystemPrincipal Monitoring(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.Monitoring)
            ? principal
            : throw new ArgumentException(
                "The watch runs as a system principal that may monitor.",
                nameof(context));
}
