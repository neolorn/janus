using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Requests;
using Xunit;
using Xunit.Sdk;

namespace Janus.Privacy.Tests.Requests;

/// <summary>
/// The daily look at whether the holiday list still reaches past
/// <c>maintenance.expiry.warninglead</c> (PRIV-RIGHT-002, D-142, OPS-ALERT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class HolidayListWatchTests
{
    // Noon in Cairo on a Thursday; thirty days on is 24 October.
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

    private readonly ConfigurationInMemory _configuration = new();
    private readonly PrivacyAlertsInMemory _alerts = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment in Cairo on the default lead.
    /// </summary>
    public HolidayListWatchTests() =>
        _configuration.Set(Settings.PrivacyCalendarTimeZone, "Africa/Cairo");

    private HolidayListWatch Watch =>
        new(new WorkingCalendar(_configuration), _configuration, _alerts, _clock);

    /// <summary>
    /// OPS-ALERT-001, D-142: a list whose last date lies inside the lead has run out,
    /// and <c>holiday-list-exhausted</c> is raised naming no one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AC1_AHolidayListRunningOutIsRaisedAsync()
    {
        Listed(new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 24));

        Assert.True(await WatchedAsync());

        PrivacyAlertRaised raised = Assert.Single(_alerts.Raised);

        Assert.Equal(AlertCondition.HolidayListExhausted, raised.Condition);
        Assert.Null(raised.Scope);
        Assert.Equal(Noon.AddDays(30), raised.Details["horizon"].GetDateTimeOffset());
    }

    /// <summary>
    /// PRIV-RIGHT-002, D-142: the list is empty by default and lists no date beyond
    /// the lead, so an empty list is raised too.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AnEmptyHolidayListIsRaisedAsync()
    {
        Assert.True(await WatchedAsync());
        Assert.Equal(AlertCondition.HolidayListExhausted, Assert.Single(_alerts.Raised).Condition);
    }

    /// <summary>
    /// PRIV-RIGHT-002, D-142: one listed date beyond the lead is enough, and nothing is
    /// raised.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AListReachingPastTheLeadRaisesNothingAsync()
    {
        Listed(new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 25));

        Assert.False(await WatchedAsync());
        Assert.Empty(_alerts.Raised);
    }

    /// <summary>
    /// PRIV-RIGHT-002, D-142: the lead is <c>maintenance.expiry.warninglead</c>, so a
    /// deployment that wants longer notice is told sooner.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_TheLeadIsTheConfiguredOneAsync()
    {
        Listed(new DateOnly(2026, 11, 3));

        Assert.False(await WatchedAsync());

        _configuration.Set(Settings.MaintenanceExpiryWarningLead, TimeSpan.FromDays(60));

        Assert.True(await WatchedAsync());
        Assert.Equal(AlertCondition.HolidayListExhausted, Assert.Single(_alerts.Raised).Condition);
    }

    private void Listed(params DateOnly[] holidays) =>
        _configuration.Set<IReadOnlyList<DateOnly>>(Settings.PrivacyHolidays, holidays);

    private async Task<bool> WatchedAsync() =>
        (await Watch.WatchAsync(TestContext.Current.CancellationToken))
        .Match(value => value, error => throw new XunitException(error.Code.ToString()));
}
