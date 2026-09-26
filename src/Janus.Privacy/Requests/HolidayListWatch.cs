using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Privacy.Requests;

/// <summary>
/// The daily look at whether the holiday list still reaches past
/// <c>maintenance.expiry.warninglead</c>.
/// </summary>
/// <param name="calendar">Where the holidays are read, in the deployment's zone.</param>
/// <param name="configuration">Where the lead is read.</param>
/// <param name="alerts">Where the condition is raised.</param>
/// <param name="time">The clock the lead is measured against.</param>
/// <remarks>
/// Implements PRIV-RIGHT-002, D-142 and OPS-ALERT-001. A list that runs out refuses
/// nothing, because an unlisted holiday only makes a deadline earlier; it is raised so
/// that the dates announced next are listed before a deadline reaches them. An empty
/// list lists no date beyond the lead, so it is raised too.
/// </remarks>
internal sealed class HolidayListWatch(
    WorkingCalendar calendar,
    IConfigurationStore configuration,
    IPrivacyAlerts alerts,
    TimeProvider time)
{
    /// <summary>
    /// Runs one look.
    /// </summary>
    /// <param name="cancellationToken">Abandons the look.</param>
    /// <returns>Whether the list had run out, or the refusal the settings gave.</returns>
    public async ValueTask<Result<bool>> WatchAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan lead = (await configuration
                .ReadAsync(Settings.MaintenanceExpiryWarningLead, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        DateTimeOffset horizon = time.GetUtcNow() + lead;

        bool listed = (await calendar.ListedBeyondAsync(horizon, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        if (listed)
        {
            return Result.Success(false);
        }

        await alerts
            .RaiseAsync(AlertCondition.HolidayListExhausted, scope: null, Reaching(horizon), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(true);
    }

    private static Dictionary<string, JsonElement> Reaching(DateTimeOffset horizon) =>
        new(capacity: 1, StringComparer.Ordinal)
        {
            ["horizon"] = JsonSerializer.SerializeToElement(horizon),
        };

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
