using System;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The prepaid gateway account: read on a schedule, watched for an abnormal drain,
/// and a hard stop under ordinary sends at the floor (AUTH-ABUSE-006, INT-SMS-004,
/// OPS-ALERT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class SmsBalanceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfigurationInMemory _configuration = new();
    private readonly SmsTransportInMemory _sms = new();
    private readonly SmsBalanceLedgerInMemory _readings = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment that has named the floor, which has no default.
    /// </summary>
    public SmsBalanceTests() => _configuration.Set(Settings.AbuseSmsBalanceFloor, 100m);

    private SmsBalance Balance =>
        new(_configuration, _sms, _readings, _work, _events, _clock);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _work.DisposeAsync();

    /// <summary>
    /// AUTH-ABUSE-006 AC1 and INT-SMS-004 AC1: an hour's spend above the drain
    /// factor times the trailing weekly mean raises the alert with no one watching.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_006_AC1_AnAbnormalHourOfSpendRaisesTheAlertAsync()
    {
        _configuration.Set(Settings.AbuseSmsDrainFactor, 3.0m);

        Steady(from: 168, to: 1, start: 100_000m, spendAnHour: 10m);

        _sms.Balance = 98_320m - 400m;

        Assert.Equal(_sms.Balance, await PolledAsync());

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.SmsBalance, raised.Condition);
        Assert.Equal(AlertSeverity.Normal, raised.Severity);
    }

    /// <summary>
    /// AUTH-ABUSE-006 AC1: an ordinary hour raises nothing, so the alert means
    /// something when it arrives.
    /// </summary>
    [Fact]
    public async Task PollAsync_AnOrdinaryHourOfSpend_RaisesNothingAsync()
    {
        Steady(from: 168, to: 1, start: 100_000m, spendAnHour: 10m);

        _sms.Balance = 98_320m - 10m;

        await PolledAsync();

        Assert.Empty(_events.Of<AlertRaised>());
    }

    /// <summary>
    /// AUTH-ABUSE-006 AC1 and INT-SMS-004 AC1: a balance that reaches the floor
    /// inside a day at the current rate raises the alert before it gets there.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_006_AC1_AFloorWithinADayRaisesTheAlertAsync()
    {
        _readings.Given(
            new BalanceReading(Noon - TimeSpan.FromMinutes(30), 350m),
            new BalanceReading(Noon - TimeSpan.FromMinutes(15), 300m));

        _sms.Balance = 250m;

        await PolledAsync();

        Assert.Equal(AlertCondition.SmsBalance, Assert.Single(_events.Of<AlertRaised>()).Condition);
    }

    /// <summary>
    /// AUTH-ABUSE-006 AC2 and INT-SMS-004 AC2: at the floor, the hard stop is in
    /// force and the condition is reported.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_006_AC2_AtTheFloorTheHardStopIsInForceAsync()
    {
        _sms.Balance = 100m;

        Assert.True(await BelowAsync());
        Assert.Equal(AlertCondition.SmsBalance, Assert.Single(_events.Of<AlertRaised>()).Condition);

        _sms.Balance = 101m;
        _clock.Advance(TimeSpan.FromHours(1));

        Assert.False(await BelowAsync());
    }

    /// <summary>
    /// A reading inside the poll interval answers the hard stop without asking the
    /// gateway again, so the question costs one call an interval.
    /// </summary>
    [Fact]
    public async Task BelowFloorAsync_AReadingInsideTheInterval_DoesNotAskTheGatewayAsync()
    {
        _readings.Given(new BalanceReading(Noon - TimeSpan.FromMinutes(5), 500m));

        Assert.False(await BelowAsync());
        Assert.Equal(0, _sms.Reads);
    }

    private async Task<decimal> PolledAsync() =>
        (await Balance.PollAsync(TestContext.Current.CancellationToken)).Match(
            balance => balance,
            error => throw new Xunit.Sdk.XunitException($"The poll failed: {error.Code}."));

    private async Task<bool> BelowAsync() =>
        (await Balance.BelowFloorAsync(TestContext.Current.CancellationToken)).Match(
            below => below,
            error => throw new Xunit.Sdk.XunitException($"The read failed: {error.Code}."));

    // A week of hourly readings falling by a steady amount, which is the mean the
    // drain factor is measured against.
    private void Steady(int from, int to, decimal start, decimal spendAnHour)
    {
        for (int hour = from; hour >= to; hour--)
        {
            _readings.Given(new BalanceReading(
                Noon - TimeSpan.FromHours(hour),
                start - (spendAnHour * (from - hour))));
        }
    }
}
