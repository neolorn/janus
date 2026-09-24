using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authorization.Gate;

/// <summary>
/// Counts the records each person is given and raises the day they read far beyond
/// their own pattern.
/// </summary>
/// <param name="store">Where the counts and the daily means are kept.</param>
/// <param name="configuration">Where the zone, the window, the factor and the minimum are read.</param>
/// <param name="alerts">Where an anomaly is raised.</param>
/// <param name="work">The one transaction the means are recomputed in.</param>
/// <param name="time">The clock the calendar day is read from.</param>
/// <remarks>
/// Implements OPS-ALERT-005 (D-045, D-153). A day is raised when its count exceeds
/// <c>exfiltration.readvolume.factor</c> times the person's daily mean over
/// <c>exfiltration.readvolume.baselinewindow</c> and exceeds
/// <c>exfiltration.readvolume.minimum</c>. The mean is recomputed once a day by the
/// <c>read-volume-baseline</c> job, so a person whose normal is high is judged against
/// that normal and not raised all day.
/// </remarks>
internal sealed class ReadVolume(
    IReadVolumeStore store,
    IConfigurationStore configuration,
    IAccessAlerts alerts,
    IUnitOfWork work,
    TimeProvider time) : IReadVolume
{
    /// <inheritdoc/>
    public async ValueTask<Result> ReturnedAsync(
        AccessContext context,
        int records,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (records < 0)
        {
            return Result.Failure(Error.From(
                ErrorCodes.RequestMalformed,
                "member",
                JsonSerializer.SerializeToElement("records")));
        }

        if (context.Acting is not SubjectId actor || records is 0)
        {
            return Result.Success();
        }

        Result<DateOnly> today = await TodayAsync(cancellationToken).ConfigureAwait(false);

        if (today.Match(_ => (Error?)null, error => error) is Error unread)
        {
            return Result.Failure(unread);
        }

        long counted = await store
            .AddAsync(actor, today.Match(day => day, _ => default), records, cancellationToken)
            .ConfigureAwait(false);

        // An unreadable setting does not silence the condition: its default stands.
        bool alerting = (await configuration
                .ReadAsync(Settings.ExfiltrationReadVolumeAlerting, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.ExfiltrationReadVolumeAlerting.Default);

        decimal factor = (await configuration
                .ReadAsync(Settings.ExfiltrationReadVolumeFactor, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.ExfiltrationReadVolumeFactor.Default);

        int minimum = (await configuration
                .ReadAsync(Settings.ExfiltrationReadVolumeMinimum, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.ExfiltrationReadVolumeMinimum.Default);

        decimal mean = await store.BaselineAsync(actor, cancellationToken).ConfigureAwait(false);

        if (alerting && counted > minimum && counted > factor * mean)
        {
            await alerts
                .RaiseAsync(
                    AlertCondition.ReadVolumeAnomaly,
                    actor.ToString(),
                    new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["actor"] = JsonSerializer.SerializeToElement(actor.ToString()),
                        ["records"] = JsonSerializer.SerializeToElement(counted),
                        ["dailyMean"] = JsonSerializer.SerializeToElement(mean),
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success();
    }

    /// <summary>
    /// Recomputes every person's daily mean over the window before today and forgets
    /// the counts older than the window.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many people have a mean, or the refusal the settings gave.</returns>
    public async ValueTask<Result<int>> RebaselineAsync(CancellationToken cancellationToken)
    {
        Result<DateOnly> today = await TodayAsync(cancellationToken).ConfigureAwait(false);

        if (today.Match(_ => (Error?)null, error => error) is Error unread)
        {
            return Result.Failure<int>(unread);
        }

        TimeSpan window = (await configuration
                .ReadAsync(Settings.ExfiltrationReadVolumeBaselineWindow, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.ExfiltrationReadVolumeBaselineWindow.Default);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        int baselined = await store
            .RebaselineAsync(
                today.Match(day => day, _ => default),
                Math.Max(1, (int)window.TotalDays),
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(baselined);
    }

    // OPS-ALERT-005, D-153: a day is the calendar day in the deployment's zone, the
    // same day the privacy clock counts.
    private async ValueTask<Result<DateOnly>> TodayAsync(CancellationToken cancellationToken)
    {
        Result<string> zone = await configuration
            .ReadAsync(Settings.PrivacyCalendarTimeZone, cancellationToken)
            .ConfigureAwait(false);

        if (zone.Match(_ => (Error?)null, error => error) is Error unread)
        {
            return Result.Failure<DateOnly>(unread);
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone.Match(value => value, _ => string.Empty), out TimeZoneInfo? found))
        {
            return Result.Failure<DateOnly>(Error.From(
                ErrorCodes.ConfigurationValueNotAllowed,
                "key",
                JsonSerializer.SerializeToElement(Settings.PrivacyCalendarTimeZone.Key)));
        }

        return Result.Success(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), found).DateTime));
    }
}
