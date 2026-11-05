using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Requests;
using Xunit;
using Xunit.Sdk;

namespace Janus.Privacy.Tests.Requests;

/// <summary>
/// The working-day clock: the declared week, the holidays as currently listed, and
/// the deployment zone a calendar day begins and ends in.
/// </summary>
[Trait("kind", "unit")]
public sealed class WorkingCalendarTests
{
    private const string Cairo = "Africa/Cairo";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(Cairo);

    private readonly ConfigurationInMemory _configuration = new();

    /// <summary>
    /// A deployment on the default Sunday to Thursday week, in Cairo.
    /// </summary>
    public WorkingCalendarTests() =>
        _configuration.Set(Settings.PrivacyCalendarTimeZone, Cairo);

    private WorkingCalendar Calendar => new(_configuration);

    /// <summary>
    /// PRIV-RIGHT-002 AC1, D-153: the six working days are the six that follow the
    /// submission's own calendar day, and the deadline is the end of the sixth.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_TheDeadlineIsTheEndOfTheSixthWorkingDayAsync()
    {
        DateTimeOffset due = await DueAsync(new DateOnly(2026, 9, 20), workingDays: 6);

        Assert.Equal(new DateOnly(2026, 9, 28), DateOnly.FromDateTime(InCairo(due).DateTime));
        Assert.Equal(new TimeSpan(23, 59, 59), InCairo(due).TimeOfDay);
    }

    /// <summary>
    /// PRIV-RIGHT-002: the week the deployment declared is the week counted, never a
    /// Monday to Friday assumption.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_TheDeclaredWeekIsTheWeekCountedAsync()
    {
        DateTimeOffset sundayToThursday = await DueAsync(new DateOnly(2026, 9, 17), workingDays: 6);

        _configuration.Set<IReadOnlySet<DayOfWeek>>(
            Settings.PrivacyWorkingDays,
            new HashSet<DayOfWeek>
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
            });

        DateTimeOffset mondayToFriday = await DueAsync(new DateOnly(2026, 9, 17), workingDays: 6);

        Assert.Equal(
            new DateOnly(2026, 9, 27),
            DateOnly.FromDateTime(InCairo(sundayToThursday).DateTime));
        Assert.Equal(
            new DateOnly(2026, 9, 25),
            DateOnly.FromDateTime(InCairo(mondayToFriday).DateTime));
    }

    /// <summary>
    /// PRIV-RIGHT-002, D-142: a holiday on the list is not a working day, so the
    /// deadline moves later; the calendar is read when the deadline is counted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_AListedHolidayPushesTheDeadlineLaterAsync()
    {
        DateTimeOffset before = await DueAsync(new DateOnly(2026, 9, 20), workingDays: 6);

        _configuration.Set<IReadOnlyList<DateOnly>>(
            Settings.PrivacyHolidays,
            [new DateOnly(2026, 9, 23)]);

        DateTimeOffset after = await DueAsync(new DateOnly(2026, 9, 20), workingDays: 6);

        Assert.Equal(new DateOnly(2026, 9, 28), DateOnly.FromDateTime(InCairo(before).DateTime));
        Assert.Equal(new DateOnly(2026, 9, 29), DateOnly.FromDateTime(InCairo(after).DateTime));
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC2, D-153: the warning is due at the start of the working day
    /// the lead names, counted back from the deadline.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC2_TheWarningIsTwoWorkingDaysBeforeTheDeadlineAsync()
    {
        Result<DateTimeOffset> warn = await Calendar
            .WarnAtAsync(new DateOnly(2026, 9, 20), workingDays: 6, lead: 2, CancellationToken.None);

        DateTimeOffset at = warn.Match(value => value, error => throw new XunitException(error.Code.ToString()));

        Assert.Equal(new DateOnly(2026, 9, 24), DateOnly.FromDateTime(InCairo(at).DateTime));
        Assert.Equal(TimeSpan.Zero, InCairo(at).TimeOfDay);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC2, D-153: the High alert is due at midnight at the head of
    /// the deadline day.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC2_TheEscalationIsMidnightOnTheDeadlineDayAsync()
    {
        DateTimeOffset due = await DueAsync(new DateOnly(2026, 9, 20), workingDays: 6);

        Result<DateTimeOffset> day = await Calendar.DayOfAsync(due, CancellationToken.None);
        DateTimeOffset at = day.Match(value => value, error => throw new XunitException(error.Code.ToString()));

        Assert.Equal(DateOnly.FromDateTime(InCairo(due).DateTime), DateOnly.FromDateTime(InCairo(at).DateTime));
        Assert.Equal(TimeSpan.Zero, InCairo(at).TimeOfDay);
    }

    /// <summary>
    /// D-153: the zone the deployment named is the zone a calendar day is determined
    /// in, so an instant late in the UTC day is already tomorrow in Cairo.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_TheCalendarDayIsTheOneInTheDeploymentZoneAsync()
    {
        Result<DateOnly> today = await Calendar.TodayAsync(
            new DateTimeOffset(2026, 9, 20, 22, 30, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(
            new DateOnly(2026, 9, 21),
            today.Match(value => value, error => throw new XunitException(error.Code.ToString())));
    }

    /// <summary>
    /// D-153: the zone is a required deployment value, and one the machine does not
    /// know is refused rather than guessed at.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_AZoneTheMachineDoesNotKnowIsRefusedAsync()
    {
        _configuration.Set(Settings.PrivacyCalendarTimeZone, "Nowhere/Nowhere");

        Result<DateTimeOffset> due = await Calendar
            .DueAsync(new DateOnly(2026, 9, 20), workingDays: 6, CancellationToken.None);

        Assert.Equal(
            ErrorCodes.ConfigurationValueNotAllowed,
            due.Match(_ => throw new XunitException("The zone was accepted."), error => error.Code));
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC6 (D-263): the deadline, the warning and the escalation are each
    /// the instant the zone gives, held in UTC, on either side of the zone's daylight
    /// saving change.
    /// </summary>
    /// <param name="received">The calendar date the request reached the company.</param>
    /// <param name="deadline">The end of the sixth working day after it, in UTC.</param>
    /// <param name="warning">The start of the second working day before it, in UTC.</param>
    /// <param name="escalation">The start of the deadline day, in UTC.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("2026-09-20", "2026-09-28T20:59:59Z", "2026-09-23T21:00:00Z", "2026-09-27T21:00:00Z")]
    [InlineData("2026-12-06", "2026-12-14T21:59:59Z", "2026-12-09T22:00:00Z", "2026-12-13T22:00:00Z")]
    public async Task PRIV_RIGHT_002_AC6_EveryInstantTheClockGivesIsHeldInUtcAsync(
        string received,
        string deadline,
        string warning,
        string escalation)
    {
        var day = DateOnly.Parse(received, CultureInfo.InvariantCulture);
        DateTimeOffset due = await DueAsync(day, workingDays: 6);
        DateTimeOffset warnAt = (await Calendar.WarnAtAsync(day, workingDays: 6, lead: 2, CancellationToken.None))
            .Match(value => value, error => throw new XunitException(error.Code.ToString()));
        DateTimeOffset escalateAt = (await Calendar.DayOfAsync(due, CancellationToken.None))
            .Match(value => value, error => throw new XunitException(error.Code.ToString()));

        Assert.Equal(
            (DateTimeOffset.Parse(deadline, CultureInfo.InvariantCulture), TimeSpan.Zero),
            (due, due.Offset));
        Assert.Equal(
            (DateTimeOffset.Parse(warning, CultureInfo.InvariantCulture), TimeSpan.Zero),
            (warnAt, warnAt.Offset));
        Assert.Equal(
            (DateTimeOffset.Parse(escalation, CultureInfo.InvariantCulture), TimeSpan.Zero),
            (escalateAt, escalateAt.Offset));
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC6 (D-263): the zone observes daylight saving, so the instants
    /// above are counted on both of its offsets and no offset is assumed fixed.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_002_AC6_TheZoneObservesDaylightSaving() =>
        Assert.NotEqual(
            Zone.GetUtcOffset(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero)),
            Zone.GetUtcOffset(new DateTimeOffset(2026, 12, 14, 12, 0, 0, TimeSpan.Zero)));

    private static DateTimeOffset InCairo(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone);

    private async Task<DateTimeOffset> DueAsync(DateOnly received, int workingDays)
    {
        Result<DateTimeOffset> due = await Calendar
            .DueAsync(received, workingDays, CancellationToken.None);

        return due.Match(value => value, error => throw new XunitException(error.Code.ToString()));
    }
}
