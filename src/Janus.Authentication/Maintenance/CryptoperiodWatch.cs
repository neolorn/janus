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
/// The daily look at whether the key-encryption key's cryptoperiod ends inside
/// <c>maintenance.expiry.warninglead</c>.
/// </summary>
/// <param name="store">Where the rotations are read.</param>
/// <param name="configuration">Where the lead is read.</param>
/// <param name="alerts">Where the warning goes.</param>
/// <param name="time">The clock the lead is measured against.</param>
/// <remarks>
/// Implements DR-009a AC1 and AC6 and OPS-ALERT-001 (D-166, D-225). The cryptoperiod is
/// measured from the key's own record: it ends a year after the latest completed
/// rotation of the key-encryption key, or, where none has completed, a year after
/// bootstrap. The warning depends on no one remembering: it is raised at every look from
/// the lead before its end until a rotation completes. OPS-ALERT-002 keeps it to one
/// alert inside its window.
/// </remarks>
internal sealed class CryptoperiodWatch(
    ICryptoperiodStore store,
    IConfigurationStore configuration,
    IAlertRaising alerts,
    TimeProvider time)
{
    /// <summary>
    /// The name of the job that runs the looks, and the scope the warning is raised under.
    /// </summary>
    public const string Job = "kek-cryptoperiod";

    // DR-009a: the cryptoperiod is a year.
    private const int CryptoperiodYears = 1;

    /// <summary>
    /// Runs one look.
    /// </summary>
    /// <param name="context">The system principal the watch runs as.</param>
    /// <param name="cancellationToken">Abandons the look.</param>
    /// <returns>Whether the cryptoperiod's end was raised as due, or the failure that stopped the look.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may monitor.</exception>
    public async ValueTask<Result<bool>> WatchAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Monitoring(context);

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

        // DR-009a AC6: the cryptoperiod follows the key's own rotation record, so a
        // rotation of the fingerprint key does not end it. A deployment never
        // bootstrapped has no key to warn of.
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
                .RaiseAsync(Alerts.Scoped(AlertCondition.ExpiryApproaching, Job, now, details), cancellationToken)
                .ConfigureAwait(false))
            .Match(() => Result.Success(true), Result.Failure<bool>);
    }

    // INF-BG-002 AC1, IDN-PRIN-001 AC3 (D-166, 304): the watch runs as a named
    // principal that may monitor, and never as nobody.
    private static SystemPrincipal Monitoring(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.Monitoring)
            ? principal
            : throw new ArgumentException(
                "The watch runs as a system principal that may monitor.",
                nameof(context));

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
