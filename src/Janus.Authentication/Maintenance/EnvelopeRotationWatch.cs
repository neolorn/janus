using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Maintenance;

/// <summary>
/// The daily look at whether the annual operation on the envelope, in which the
/// key-encryption key is rotated, falls due inside
/// <c>maintenance.expiry.warninglead</c>.
/// </summary>
/// <param name="store">Where the maintenance log is kept.</param>
/// <param name="configuration">Where the lead is read.</param>
/// <param name="alerts">Where the warning goes.</param>
/// <param name="time">The clock the lead is measured against.</param>
/// <remarks>
/// Implements DR-009a AC1, OPS-MAINT-001 and OPS-ALERT-001, as entry 341 of the
/// decisions pending review settles them. The key-encryption key's cryptoperiod is the
/// cadence of the operation it is rotated in, a year from the last one the log records.
/// The warning does not depend on anyone remembering: it is raised at every look from
/// the lead before that anniversary until the next operation is recorded, and a log
/// that records none has the operation due now. OPS-ALERT-002 keeps it to one alert
/// inside its window.
/// </remarks>
internal sealed class EnvelopeRotationWatch(
    IMaintenanceStore store,
    IConfigurationStore configuration,
    IAlertChannels alerts,
    TimeProvider time)
{
    // Chapter 06 section 9: the operation is annual.
    private const int CryptoperiodYears = 1;

    private static readonly string Scope = WrittenName.Of(MaintenanceTask.EnvelopeRotation);

    /// <summary>
    /// Runs one look.
    /// </summary>
    /// <param name="cancellationToken">Abandons the look.</param>
    /// <returns>Whether the operation was raised as due, or the failure that stopped the look.</returns>
    public async ValueTask<Result<bool>> WatchAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan lead = (await configuration
                .ReadAsync(Settings.MaintenanceExpiryWarningLead, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        DateTimeOffset? performedAt = (await store.LogAsync(cancellationToken).ConfigureAwait(false))
            .Where(entry => entry.Task is MaintenanceTask.EnvelopeRotation)
            .Select(entry => (DateTimeOffset?)entry.PerformedAt)
            .Max();
        DateTimeOffset? dueAt = performedAt?.AddYears(CryptoperiodYears);

        if (dueAt is DateTimeOffset due && due - lead > now)
        {
            return Result.Success(false);
        }

        return (await alerts
                .RaiseAsync(
                    Alerts.Of(AlertCondition.ExpiryApproaching, Scope, now, Due(performedAt, dueAt)),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(() => Result.Success(true), Result.Failure<bool>);
    }

    private static Dictionary<string, JsonElement> Due(DateTimeOffset? performedAt, DateTimeOffset? dueAt) =>
        new(capacity: 3, StringComparer.Ordinal)
        {
            ["task"] = JsonSerializer.SerializeToElement(Scope),
            ["performedAt"] = JsonSerializer.SerializeToElement(performedAt),
            ["dueAt"] = JsonSerializer.SerializeToElement(dueAt),
        };

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
