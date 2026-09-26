using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Tests;
using Janus.Authorization.Gate;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Authorization;
using Xunit;
using Xunit.Sdk;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The records each person is given, counted by day and judged against that person's
/// own daily mean (OPS-ALERT-005, D-153).
/// </summary>
[Trait("kind", "unit")]
public sealed class ReadVolumeTests : IAsyncDisposable
{
    // Noon in Cairo on Thursday 24 September.
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = new(2026, 9, 24);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly ReadVolumeStoreInMemory _store = new();
    private readonly EventsInMemory _alerts = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment in Cairo on the default factor, minimum and window.
    /// </summary>
    public ReadVolumeTests() =>
        _configuration.Set(Settings.PrivacyCalendarTimeZone, "Africa/Cairo");

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _randomness.Dispose();
        await _work.DisposeAsync();
    }

    private ReadVolume Volume =>
        new(_store, _configuration, new AccessAlerts(_alerts, _clock), _work, _clock);

    /// <summary>
    /// OPS-ALERT-005 AC1, D-153: a person whose mean is a hundred records a day and who
    /// is given six hundred today is raised, once the count passes both three times the
    /// mean and the minimum, naming the person and not what they read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_AC1_AnActorReadingFarBeyondTheirOwnPatternRaisesAsync()
    {
        var clerk = SubjectId.New(_randomness);
        await ReadDailyAsync(clerk, 100);

        await ReturnedAsync(AccessContext.Of(clerk), 300);

        Assert.Empty(_alerts.Of<AlertRaised>());

        await ReturnedAsync(AccessContext.Of(clerk), 300);

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(AlertCondition.ReadVolumeAnomaly, raised.Condition);
        Assert.Equal(
            Alerts.Key(AlertCondition.ReadVolumeAnomaly, clerk.ToString()),
            Alerts.Deduplication(raised.IdempotencyKey));
        Assert.Equal(
            ["actor", "dailyMean", "records"],
            raised.Details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(clerk.ToString(), raised.Details["actor"].GetString());
        Assert.Equal(600, raised.Details["records"].GetInt64());
        Assert.Equal(100m, raised.Details["dailyMean"].GetDecimal());
    }

    /// <summary>
    /// OPS-ALERT-005 AC2, D-153: a person whose normal is two thousand records a day is
    /// judged against that normal, so an ordinary day and a busy one below three times
    /// it raise nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_AC2_AnActorWhoseNormalIsHighDoesNotAlertAsync()
    {
        var manager = SubjectId.New(_randomness);
        await ReadDailyAsync(manager, 2000);

        await ReturnedAsync(AccessContext.Of(manager), 2000);
        await ReturnedAsync(AccessContext.Of(manager), 3900);

        Assert.Empty(_alerts.Of<AlertRaised>());
        Assert.Equal(5900, _store.Counted[(manager, Today)]);
    }

    /// <summary>
    /// OPS-ALERT-005, D-153: a person with no history has a mean of nothing, and the
    /// minimum is what keeps their first busy day silent until it is passed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_TheMinimumKeepsAFirstBusyDaySilentAsync()
    {
        var newcomer = SubjectId.New(_randomness);

        await ReturnedAsync(AccessContext.Of(newcomer), 500);

        Assert.Empty(_alerts.Of<AlertRaised>());

        await ReturnedAsync(AccessContext.Of(newcomer), 1);

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(501, raised.Details["records"].GetInt64());
        Assert.Equal(0m, raised.Details["dailyMean"].GetDecimal());
    }

    /// <summary>
    /// OPS-ALERT-005, D-153: the factor and the minimum are the configured ones.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_TheFactorAndMinimumAreTheConfiguredOnesAsync()
    {
        var clerk = SubjectId.New(_randomness);
        await ReadDailyAsync(clerk, 100);
        _configuration.Set(Settings.ExfiltrationReadVolumeFactor, 10m);
        _configuration.Set(Settings.ExfiltrationReadVolumeMinimum, 50);

        await ReturnedAsync(AccessContext.Of(clerk), 1000);

        Assert.Empty(_alerts.Of<AlertRaised>());

        await ReturnedAsync(AccessContext.Of(clerk), 1);

        Assert.Equal(AlertCondition.ReadVolumeAnomaly, Assert.Single(_alerts.Of<AlertRaised>()).Condition);
    }

    /// <summary>
    /// OPS-ALERT-005, D-045: with <c>exfiltration.readvolume.alerting</c> off the
    /// records are still counted, so the mean is whole when it is turned back on, and
    /// nothing is raised.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_AlertingOffCountsAndRaisesNothingAsync()
    {
        var clerk = SubjectId.New(_randomness);
        _configuration.Set(Settings.ExfiltrationReadVolumeAlerting, false);

        await ReturnedAsync(AccessContext.Of(clerk), 10_000);

        Assert.Empty(_alerts.Of<AlertRaised>());
        Assert.Equal(10_000, _store.Counted[(clerk, Today)]);
    }

    /// <summary>
    /// OPS-ALERT-005, D-153: the reading is the actor's, so a staff member acting for
    /// another person is counted and the person acted for is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_TheActorIsCountedNotThePersonActedForAsync()
    {
        var agent = SubjectId.New(_randomness);
        var customer = SubjectId.New(_randomness);

        await ReturnedAsync(AccessContext.Of(agent, customer), 40);

        Assert.Equal((agent, Today), Assert.Single(_store.Counted.Keys));
    }

    /// <summary>
    /// OPS-ALERT-005, D-153: work a system principal does is nobody's reading, and
    /// nothing is counted for it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_ASystemPrincipalIsNotCountedAsync()
    {
        var sweep = AccessContext.Of(
            SystemPrincipal.ForDeployment("expiry-sweep", "OPS-OBS-003", SystemOperation.ExpirySweep));

        await ReturnedAsync(sweep, 100_000);

        Assert.Empty(_store.Counted);
        Assert.Empty(_alerts.Of<AlertRaised>());
    }

    /// <summary>
    /// OPS-ALERT-005, CONV-API-003: a negative count is refused naming the member, and
    /// nothing is counted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_ANegativeCountIsMalformedAsync()
    {
        Result refused = await Volume.ReturnedAsync(
            AccessContext.Of(SubjectId.New(_randomness)),
            -1,
            TestContext.Current.CancellationToken);

        Error error = refused.Match(() => throw new XunitException("A negative count was admitted."), error => error);

        Assert.Equal(ErrorCodes.RequestMalformed, error.Code);
        Assert.Equal("records", error.Details["member"].GetString());
        Assert.Empty(_store.Counted);
    }

    /// <summary>
    /// OPS-ALERT-005, D-153: a day is the calendar day in
    /// <c>privacy.calendar.timezone</c>, so a read at half past ten in the evening UTC
    /// is counted on the next day in Cairo.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_ADayIsTheCalendarDayInTheZoneAsync()
    {
        var clerk = SubjectId.New(_randomness);
        _clock.Advance(TimeSpan.FromHours(13.5));

        await ReturnedAsync(AccessContext.Of(clerk), 7);

        Assert.Equal((clerk, Today.AddDays(1)), Assert.Single(_store.Counted.Keys));
    }

    /// <summary>
    /// OPS-ALERT-005, D-071, D-153: the mean is taken over every day of the window
    /// before today, a day without reads counting as nothing read, and a count older
    /// than the window is forgotten; the recount runs in one transaction.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_TheMeanIsTakenOverTheWindowBeforeTodayAsync()
    {
        var clerk = SubjectId.New(_randomness);
        _store.Read(clerk, Today.AddDays(-31), 90_000);
        _store.Read(clerk, Today.AddDays(-10), 3000);
        _store.Read(clerk, Today, 60_000);

        Assert.Equal(1, await RebaselinedAsync());
        Assert.Equal(1, _work.Committed);
        Assert.False(_store.Counted.ContainsKey((clerk, Today.AddDays(-31))));

        await ReturnedAsync(AccessContext.Of(clerk), 1);

        Assert.Equal(100m, Assert.Single(_alerts.Of<AlertRaised>()).Details["dailyMean"].GetDecimal());
    }

    /// <summary>
    /// OPS-ALERT-005, D-071: the window is <c>exfiltration.readvolume.baselinewindow</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_005_TheWindowIsTheConfiguredOneAsync()
    {
        var clerk = SubjectId.New(_randomness);
        _store.Read(clerk, Today.AddDays(-10), 3000);
        _store.Read(clerk, Today.AddDays(-2), 3000);
        _configuration.Set(Settings.ExfiltrationReadVolumeBaselineWindow, TimeSpan.FromDays(5));

        Assert.Equal(1, await RebaselinedAsync());

        await ReturnedAsync(AccessContext.Of(clerk), 1801);

        Assert.Equal(600m, Assert.Single(_alerts.Of<AlertRaised>()).Details["dailyMean"].GetDecimal());
    }

    // The person read the same count on each of the thirty days before today.
    private async Task ReadDailyAsync(SubjectId actor, long records)
    {
        for (int day = 1; day <= 30; day++)
        {
            _store.Read(actor, Today.AddDays(-day), records);
        }

        Assert.Equal(1, await RebaselinedAsync());
    }

    private async Task ReturnedAsync(AccessContext context, int records) =>
        Assert.Null((await Volume.ReturnedAsync(context, records, TestContext.Current.CancellationToken))
            .Match(() => (Error?)null, error => error));

    private async Task<int> RebaselinedAsync() =>
        (await Volume.RebaselineAsync(TestContext.Current.CancellationToken))
        .Match(value => value, error => throw new XunitException(error.Code.ToString()));
}
