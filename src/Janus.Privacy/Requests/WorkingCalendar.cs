using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Privacy.Requests;

/// <summary>
/// The deployment's own week: which days are worked, which are holidays, and in
/// which zone a calendar day begins and ends.
/// </summary>
/// <param name="configuration">Where the week, the holidays and the zone are read.</param>
/// <remarks>
/// Implements PRIV-RIGHT-002 and D-142. The calendar is read at the moment a deadline
/// is counted and never held, so a holiday announced after a request arrived is
/// honoured for it. A holiday missing from the list counts as a working day and
/// yields an earlier deadline, which is always compliant, so the list running out
/// refuses nothing.
/// </remarks>
internal sealed class WorkingCalendar(IConfigurationStore configuration)
{
    /// <summary>
    /// The end of the day, one tick short of midnight, which is what a deadline
    /// falls on.
    /// </summary>
    private static readonly TimeSpan EndOfDay = new(23, 59, 59);

    /// <summary>
    /// The deadline a submission on one date runs to.
    /// </summary>
    /// <param name="received">The calendar date the request reached the company.</param>
    /// <param name="workingDays">How many working days the decision has.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The instant the decision is due by, or the refusal the settings gave.</returns>
    public async ValueTask<Result<DateTimeOffset>> DueAsync(
        DateOnly received,
        int workingDays,
        CancellationToken cancellationToken) =>
        (await WeekAsync(cancellationToken).ConfigureAwait(false))
        .Match(
            week => Result.Success(End(week, Counted(week, received, workingDays))),
            Result.Failure<DateTimeOffset>);

    /// <summary>
    /// The instant at which a deadline is warned about, which is the start of the
    /// working day that many working days before it.
    /// </summary>
    /// <param name="received">The calendar date the request reached the company.</param>
    /// <param name="workingDays">How many working days the decision has.</param>
    /// <param name="lead">How many working days before the deadline to warn.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The instant the warning is due at, or the refusal the settings gave.</returns>
    public async ValueTask<Result<DateTimeOffset>> WarnAtAsync(
        DateOnly received,
        int workingDays,
        int lead,
        CancellationToken cancellationToken) =>
        (await WeekAsync(cancellationToken).ConfigureAwait(false))
        .Match(
            week => Result.Success(
                Start(week, Back(week, Counted(week, received, workingDays), lead))),
            Result.Failure<DateTimeOffset>);

    /// <summary>
    /// The start of the day a deadline falls on, which is when it is escalated.
    /// </summary>
    /// <param name="due">The instant the decision is due by.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The start of that day in the deployment's zone.</returns>
    public async ValueTask<Result<DateTimeOffset>> DayOfAsync(
        DateTimeOffset due,
        CancellationToken cancellationToken) =>
        (await WeekAsync(cancellationToken).ConfigureAwait(false))
        .Match(
            week => Result.Success(Start(
                week,
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(due, week.Zone).DateTime))),
            Result.Failure<DateTimeOffset>);

    /// <summary>
    /// The calendar date an instant falls on in the deployment's zone.
    /// </summary>
    /// <param name="instant">The instant.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The date, or the refusal the settings gave.</returns>
    public async ValueTask<Result<DateOnly>> TodayAsync(
        DateTimeOffset instant,
        CancellationToken cancellationToken) =>
        (await WeekAsync(cancellationToken).ConfigureAwait(false))
        .Match(
            week => Result.Success(
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, week.Zone).DateTime)),
            Result.Failure<DateOnly>);

    /// <summary>
    /// Whether any listed holiday falls after the calendar date an instant falls on in
    /// the deployment's zone.
    /// </summary>
    /// <param name="instant">The instant.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether one does, or the refusal the settings gave.</returns>
    public async ValueTask<Result<bool>> ListedBeyondAsync(
        DateTimeOffset instant,
        CancellationToken cancellationToken) =>
        (await WeekAsync(cancellationToken).ConfigureAwait(false))
        .Match(
            week =>
            {
                var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, week.Zone).DateTime);

                return Result.Success(week.Holidays.Any(holiday => holiday > day));
            },
            Result.Failure<bool>);

    // PRIV-RIGHT-002: the days counted are the ones that follow the submission's own
    // calendar day, so a request received on a working day does not spend it.
    private static DateOnly Counted(Week week, DateOnly received, int workingDays)
    {
        DateOnly day = received;

        for (int counted = 0; counted < workingDays;)
        {
            day = day.AddDays(1);

            if (week.Works(day))
            {
                counted++;
            }
        }

        return day;
    }

    private static DateOnly Back(Week week, DateOnly deadline, int workingDays)
    {
        DateOnly day = deadline;

        for (int counted = 0; counted < workingDays;)
        {
            day = day.AddDays(-1);

            if (week.Works(day))
            {
                counted++;
            }
        }

        return day;
    }

    private static DateTimeOffset End(Week week, DateOnly day) =>
        new(day.ToDateTime(TimeOnly.FromTimeSpan(EndOfDay)), week.Offset(day, EndOfDay));

    private static DateTimeOffset Start(Week week, DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue), week.Offset(day, TimeSpan.Zero));

    private async ValueTask<Result<Week>> WeekAsync(CancellationToken cancellationToken)
    {
        Result<string> zone = await configuration
            .ReadAsync(Settings.PrivacyCalendarTimeZone, cancellationToken)
            .ConfigureAwait(false);

        Result<IReadOnlySet<DayOfWeek>> days = await configuration
            .ReadAsync(Settings.PrivacyWorkingDays, cancellationToken)
            .ConfigureAwait(false);

        Result<IReadOnlyList<DateOnly>> holidays = await configuration
            .ReadAsync(Settings.PrivacyHolidays, cancellationToken)
            .ConfigureAwait(false);

        Error? refused = zone.Match(_ => (Error?)null, error => error)
            ?? days.Match(_ => (Error?)null, error => error)
            ?? holidays.Match(_ => (Error?)null, error => error);

        if (refused is not null)
        {
            return Result.Failure<Week>(refused);
        }

        string named = zone.Match(value => value, _ => string.Empty);

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(named, out TimeZoneInfo? found))
        {
            return Result.Failure<Week>(Error.From(
                ErrorCodes.ConfigurationValueNotAllowed,
                "key",
                JsonSerializer.SerializeToElement(Settings.PrivacyCalendarTimeZone.Key)));
        }

        return Result.Success(new Week(
            found,
            days.Match(value => value, _ => new HashSet<DayOfWeek>()),
            new HashSet<DateOnly>(holidays.Match(value => value, _ => []))));
    }

    // The deployment's week as one object, so a deadline is counted against one
    // reading of the configuration and never against two.
    private sealed record Week(
        TimeZoneInfo Zone,
        IReadOnlySet<DayOfWeek> Days,
        IReadOnlySet<DateOnly> Holidays)
    {
        public bool Works(DateOnly day) =>
            Days.Contains(day.DayOfWeek) && !Holidays.Contains(day);

        public TimeSpan Offset(DateOnly day, TimeSpan at) =>
            Zone.GetUtcOffset(day.ToDateTime(TimeOnly.FromTimeSpan(at)));
    }
}
