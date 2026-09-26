using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Alerting;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Alerting;
using Janus.Hosting.Sending;
using Xunit;

namespace Janus.Hosting.Tests.Alerting;

/// <summary>
/// What carries a raised condition to the people who have to see it: email for all
/// of them, SMS for the severe ones, one alert per condition per window, and the
/// owner where the deployment says so (OPS-ALERT-002, OPS-ALERT-003, OPS-ALERT-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class AlertRouterTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] OneLanguage = ["en"];

    private static readonly string[] TwoAddresses = ["ops@example.test", "second@example.test"];

    private static readonly string[] OneNumber = ["+201001234567"];

    private readonly ConfigurationInMemory _configuration = new();
    private readonly SendLedgerInMemory _ledger = new();
    private readonly AlertLedgerInMemory _alerts = new();
    private readonly AlertLogInMemory _log = new();
    private readonly MessageTemplatesInMemory _templates = new();
    private readonly MailTransportInMemory _mail = new();
    private readonly SmsTransportInMemory _sms = new();
    private readonly SmsBalanceLedgerInMemory _balances = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment with two addresses, one number and a floor it has named.
    /// </summary>
    public AlertRouterTests()
    {
        _configuration.Set(Settings.NotificationLanguages, OneLanguage);
        _configuration.Set(Settings.AlertingEmailDestinations, TwoAddresses);
        _configuration.Set(Settings.AlertingSmsDestinations, OneNumber);
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 100m);
        _configuration.Set(Settings.AlertingOwnerEmail, "owner@example.test");
        _configuration.Set(Settings.AlertingOwnerSms, "+201009999999");
        _sms.Balance = 1000m;
    }

    private AlertRouter Router =>
        new(
            _configuration,
            new SendingService(
                _configuration,
                _ledger,
                new SendOutboxInMemory(),
                _templates,
                _mail,
                _sms,
                RestrictionKeySuppliers.None,
                Considered.Nothing(_work, _clock),
                new SmsBalance(_configuration, _sms, _balances, _work, _events, _clock),
                _work,
                _events,
                _events,
                _clock,
                _randomness),
            _alerts,
            _work,
            _log);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// OPS-ALERT-002 AC1: a thousand failures against one account produce one alert,
    /// because the volume bound on alerting is deduplication.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_002_AC1_AThousandFailuresProduceOneAlertAsync()
    {
        for (int attempt = 0; attempt < 1000; attempt++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));

            AlertDelivery delivered = await RaisedAsync(
                Alerts.Of(AlertCondition.AuthFailuresSustained, "one-account", _clock.GetUtcNow()));

            Assert.Equal(attempt > 0, delivered.Deduplicated);
        }

        Assert.Equal(2, _mail.Taken.Count);
        Assert.Single(_sms.Taken);
    }

    /// <summary>
    /// OPS-ALERT-002 AC1: two accounts are two conditions, so one attack does not
    /// hide another.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_002_AC1_TheSameConditionOnTwoAccountsIsTwoAlertsAsync()
    {
        await RaisedAsync(Alerts.Of(AlertCondition.AuthFailuresSustained, "one", Noon));
        await RaisedAsync(Alerts.Of(AlertCondition.AuthFailuresSustained, "two", Noon));

        Assert.Equal(4, _mail.Taken.Count);
    }

    /// <summary>
    /// OPS-ALERT-003: email carries every condition and SMS carries only the severe
    /// ones, so a Normal condition does not spend the prepaid balance.
    /// </summary>
    [Fact]
    public async Task DeliverAsync_ANormalCondition_IsCarriedByEmailAloneAsync()
    {
        AlertDelivery delivered = await RaisedAsync(
            Alerts.Of(AlertCondition.RestrictionGranted, "sms.destination", Noon));

        Assert.Equal(2, delivered.Email);
        Assert.Equal(0, delivered.Sms);
        Assert.Empty(_sms.Taken);
        Assert.Empty(_log.Unreached);
    }

    /// <summary>
    /// OPS-ALERT-003 AC1: an alert about the mail system goes to the phone first, so
    /// the alert is not carried by the thing it is about.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_003_AC1_AMailSystemFailureStillReachesSomeoneAsync()
    {
        _mail.Accepts = false;

        AlertDelivery delivered = await RaisedAsync(
            Alerts.Of(AlertCondition.RelayDomainUnregistered, null, Noon));

        Assert.Equal(0, delivered.Email);
        Assert.Equal(1, delivered.Sms);
        Assert.Single(_sms.Taken);
    }

    /// <summary>
    /// OPS-ALERT-003 AC2 and AC3: the balance-floor breach is reported although
    /// ordinary sends are stopped, because an alert does not ask the floor.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_003_AC2_ABalanceFloorBreachIsReportedDespiteTheStopAsync()
    {
        _sms.Balance = 0m;

        AlertDelivery delivered = await RaisedAsync(
            Alerts.Of(AlertCondition.SmsBalance, null, Noon));

        Assert.Equal(2, delivered.Email);
        Assert.Empty(_sms.Taken);

        AlertDelivery severe = await RaisedAsync(
            Alerts.Of(AlertCondition.BreakGlassUsed, null, Noon));

        Assert.Equal(2, severe.Sms);
    }

    /// <summary>
    /// OPS-ALERT-003 AC4: the residual case of a gateway that carries nothing is
    /// recorded, with email carrying alone.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_003_AC4_AGatewayThatCarriesNothingIsRecordedAsync()
    {
        _sms.Accepts = false;

        AlertDelivery delivered = await RaisedAsync(
            Alerts.Of(AlertCondition.BreakGlassUsed, null, Noon));

        Assert.Equal(3, delivered.Email);
        Assert.Equal(0, delivered.Sms);
        Assert.True(delivered.SmsUnreachable);
        Assert.Equal(("breakglass-used", "sms"), Assert.Single(_log.Unreached));
    }

    /// <summary>
    /// OPS-ALERT-003: email reaching nobody sends the alert by phone whatever the
    /// severity, which is what the second channel exists for.
    /// </summary>
    [Fact]
    public async Task DeliverAsync_EmailReachingNobody_FallsToTheSecondChannelAsync()
    {
        _mail.Accepts = false;

        AlertDelivery delivered = await RaisedAsync(
            Alerts.Of(AlertCondition.RestrictionGranted, "sms.destination", Noon));

        Assert.Equal(0, delivered.Email);
        Assert.Equal(1, delivered.Sms);
    }

    /// <summary>
    /// OPS-ALERT-004 AC1: more than one destination can be configured per channel,
    /// and every one of them is reached.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_004_AC1_EveryConfiguredDestinationIsReachedAsync()
    {
        AlertDelivery delivered = await RaisedAsync(
            Alerts.Of(AlertCondition.AuthFailuresSustained, null, Noon));

        Assert.Equal(2, delivered.Email);
        Assert.Equal(1, delivered.Sms);
        Assert.Equal(
            TwoAddresses,
            _mail.Taken.Select(mail => mail.Destination.Value).Order(StringComparer.Ordinal));
        Assert.Equal(OneNumber[0], Assert.Single(_sms.Taken).Destination.Value);
    }

    /// <summary>
    /// OPS-ALERT-004 AC2: turning owner notification on is a stored value, so it
    /// takes effect on the next alert with nothing deployed.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_004_AC2_EnablingOwnerNotificationNeedsNoDeployAsync()
    {
        Assert.Equal(2, (await RaisedAsync(Raised(AlertCondition.RestrictionGranted))).Email);

        _configuration.Set(Settings.AlertingOwnerEnabled, true);

        Assert.Equal(3, (await RaisedAsync(Raised(AlertCondition.RestrictionLoosened))).Email);
        Assert.Contains(
            _mail.Taken,
            mail => string.Equals(mail.Destination.Value, "owner@example.test", StringComparison.Ordinal));
    }

    /// <summary>
    /// OPS-ALERT-004 AC3: with owner notification off, a break-glass use still
    /// reaches the owner's address and number.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_004_AC3_ABreakGlassUseReachesTheOwnerRegardlessAsync()
    {
        _configuration.Set(Settings.AlertingOwnerEnabled, false);

        AlertDelivery delivered = await RaisedAsync(Raised(AlertCondition.BreakGlassUsed));

        Assert.Equal(3, delivered.Email);
        Assert.Equal(2, delivered.Sms);
        Assert.Contains(
            _mail.Taken,
            mail => string.Equals(mail.Destination.Value, "owner@example.test", StringComparison.Ordinal));
        Assert.Contains(
            _sms.Taken,
            message => string.Equals(message.Destination.Value, "+201009999999", StringComparison.Ordinal));
    }

    private static AlertRaised Raised(AlertCondition condition) => Alerts.Of(condition, null, Noon);

    private async Task<AlertDelivery> RaisedAsync(AlertRaised raised) =>
        (await Router.RaiseAsync(raised, TestContext.Current.CancellationToken)).Match(
            delivered => delivered,
            error => throw new Xunit.Sdk.XunitException($"The alert was refused: {error.Code}."));
}
