using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Maintenance;

/// <summary>
/// The daily pass that warns of every licence and permit whose expiry is inside
/// <c>maintenance.expiry.warninglead</c>.
/// </summary>
/// <param name="store">Where the licences and permits are kept.</param>
/// <param name="configuration">Where the lead is read.</param>
/// <param name="alerts">Where the warning goes.</param>
/// <param name="time">The clock the lead is measured against.</param>
/// <remarks>
/// Implements OPS-MAINT-001, OPS-ALERT-001 and INF-BG-001. The warning does not depend
/// on anyone remembering: it is raised at every pass until the date is moved, and one
/// that lapsed unrenewed goes on being raised, because a lapsed licence is what the
/// warning was for. OPS-ALERT-002 keeps each one to one alert inside its window.
/// </remarks>
internal sealed class LicenceExpiry(
    IMaintenanceStore store,
    IConfigurationStore configuration,
    IAlertChannels alerts,
    TimeProvider time)
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many licences and permits were warned of, or the failure that stopped the pass.</returns>
    public async ValueTask<Result<int>> WarnAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan lead = (await configuration
                .ReadAsync(Settings.MaintenanceExpiryWarningLead, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<int>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();
        int warned = 0;

        foreach (Licence licence in await store.LicencesAsync(cancellationToken).ConfigureAwait(false))
        {
            if (licence.ExpiresAt - lead > now)
            {
                continue;
            }

            if ((await alerts
                    .RaiseAsync(
                        Alerts.Of(AlertCondition.ExpiryApproaching, "licence:" + licence.Id, now, Expiring(licence)),
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unraised)
            {
                return Result.Failure<int>(unraised);
            }

            warned++;
        }

        return Result.Success(warned);
    }

    private static Dictionary<string, JsonElement> Expiring(Licence licence) =>
        new(capacity: 4, StringComparer.Ordinal)
        {
            ["licence"] = JsonSerializer.SerializeToElement(licence.Id.ToString()),
            ["kind"] = JsonSerializer.SerializeToElement(WrittenName.Of(licence.Kind)),
            ["name"] = JsonSerializer.SerializeToElement(licence.Name),
            ["expiresAt"] = JsonSerializer.SerializeToElement(licence.ExpiresAt),
        };

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
