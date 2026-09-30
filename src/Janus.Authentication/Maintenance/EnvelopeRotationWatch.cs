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
/// The daily look at whether the annual operation on the envelope falls due inside
/// <c>maintenance.expiry.warninglead</c>, and at whether the key-encryption key's
/// cryptoperiod ends inside it.
/// </summary>
/// <param name="store">Where the maintenance log is kept and the rotations are read.</param>
/// <param name="configuration">Where the lead is read.</param>
/// <param name="alerts">Where the warning goes.</param>
/// <param name="time">The clock the lead is measured against.</param>
/// <remarks>
/// Implements DR-009a AC1 and AC6, OPS-MAINT-001 and OPS-ALERT-001 (D-166, 341). The
/// operation is due a year from the last one the log records, and a log that records
/// none has it due now. The cryptoperiod is measured from the key's own record, never
/// from the log: it ends a year after the latest completed rotation of the
/// key-encryption key, or, where none has completed, a year after bootstrap. Neither
/// warning depends on anyone remembering: each is raised at every look from the lead
/// before its end until what ends it is recorded. OPS-ALERT-002 keeps each to one alert
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

    // Chapter 10 section 5: the scope the cryptoperiod is warned of under.
    private const string Cryptoperiod = "kek-cryptoperiod";

    private static readonly string Scope = WrittenName.Of(MaintenanceTask.EnvelopeRotation);

    /// <summary>
    /// Runs one look.
    /// </summary>
    /// <param name="cancellationToken">Abandons the look.</param>
    /// <returns>
    /// Whether the operation or the cryptoperiod's end was raised as due, or the failure
    /// that stopped the look.
    /// </returns>
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
        bool operationDue = dueAt is not DateTimeOffset due || due - lead <= now;

        if (operationDue
            && (await alerts
                    .RaiseAsync(
                        Alerts.Scoped(AlertCondition.ExpiryApproaching, Scope, now, Due(performedAt, dueAt)),
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notRaised)
        {
            return Result.Failure<bool>(notRaised);
        }

        return (await CryptoperiodAsync(lead, now, cancellationToken).ConfigureAwait(false))
            .Match(ending => Result.Success(operationDue || ending), Result.Failure<bool>);
    }

    // DR-009a AC6: the cryptoperiod follows the key's own rotation record, so no log
    // entry ends the warning and a rotation of the fingerprint key does not either. A
    // deployment never bootstrapped has no key to warn of.
    private async ValueTask<Result<bool>> CryptoperiodAsync(
        TimeSpan lead,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        (int Version, DateTimeOffset CompletedAt)? rotated =
            await store.KeyEncryptionKeyRotatedAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset? rotatedAt = rotated?.CompletedAt
            ?? await store.BootstrappedAsync(cancellationToken).ConfigureAwait(false);

        if (rotatedAt is not DateTimeOffset from || from.AddYears(CryptoperiodYears) - lead > now)
        {
            return Result.Success(false);
        }

        var details = new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
        {
            ["version"] = JsonSerializer.SerializeToElement(rotated?.Version),
            ["rotatedAt"] = JsonSerializer.SerializeToElement(from),
            ["dueAt"] = JsonSerializer.SerializeToElement(from.AddYears(CryptoperiodYears)),
        };

        return (await alerts
                .RaiseAsync(Alerts.Scoped(AlertCondition.ExpiryApproaching, Cryptoperiod, now, details), cancellationToken)
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
