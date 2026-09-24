using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Alerting;

/// <summary>
/// The hourly pass that raises a failed certificate renewal.
/// </summary>
/// <param name="renewal">How the last renewal went, where the deployment registered what renews.</param>
/// <param name="alerts">Where the failure goes.</param>
/// <param name="time">The clock the alert is stamped with.</param>
/// <remarks>
/// Implements INF-TLS-003 AC2, OPS-ALERT-001 and INF-BG-001. The failure is raised at
/// every pass until a renewal succeeds, because an expired certificate is an outage of
/// every application at once; OPS-ALERT-002 keeps it to one alert inside its window, and
/// the alert carries the latest failure. A renewal whose outcome cannot be read, or a
/// deployment that registered nothing to read it from, is raised as a degradation,
/// because the renewal is then not known to work.
/// </remarks>
internal sealed class CertificateRenewalWatch(
    ICertificateRenewal? renewal,
    IAlertChannels alerts,
    TimeProvider time)
{
    private const string Absent = "certificate.renewal.absent";
    private const string Unread = "certificate.renewal.unread";

    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>Nothing, or the failure where what was found could not be raised.</returns>
    public async ValueTask<Result> WatchAsync(CancellationToken cancellationToken)
    {
        if (renewal is null)
        {
            return await alerts.RaiseAsync(Degraded(Absent), cancellationToken).ConfigureAwait(false);
        }

        Result<DateTimeOffset?> read = await renewal.LastFailureAsync(cancellationToken).ConfigureAwait(false);

        if (!read.Match(_ => true, _ => false))
        {
            return await alerts.RaiseAsync(Degraded(Unread), cancellationToken).ConfigureAwait(false);
        }

        if (read.Match(failed => failed, _ => null) is not DateTimeOffset failedAt)
        {
            return Result.Success();
        }

        return await alerts
            .RaiseAsync(
                Alerts.Of(AlertCondition.CertificateRenewalFailed, scope: null, time.GetUtcNow(), Failed(failedAt)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private AlertRaised Degraded(string scope) =>
        Alerts.Of(AlertCondition.Degradation, scope, time.GetUtcNow());

    private static Dictionary<string, JsonElement> Failed(DateTimeOffset failedAt) =>
        new(capacity: 1, StringComparer.Ordinal)
        {
            ["failedAt"] = JsonSerializer.SerializeToElement(failedAt.ToUniversalTime()),
        };
}
