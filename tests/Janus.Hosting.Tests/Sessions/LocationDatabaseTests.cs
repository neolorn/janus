using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Alerting;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Sessions;
using Xunit;

namespace Janus.Hosting.Tests.Sessions;

/// <summary>
/// What a session's city is resolved from: a local database read in process, which no
/// deployment holds yet, so no location is shown and the degradation is raised
/// (INT-GEN-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class LocationDatabaseTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] OneLanguage = ["en"];

    private static readonly string[] OneAddress = ["ops@example.test"];

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
    /// A deployment with one address to alert and nothing else to say.
    /// </summary>
    public LocationDatabaseTests()
    {
        _configuration.Set(Settings.NotificationLanguages, OneLanguage);
        _configuration.Set(Settings.AlertingEmailDestinations, OneAddress);
        _configuration.Set(Settings.AlertingSmsDestinations, OneNumber);
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 100m);
        _sms.Balance = 1000m;
    }

    private LocationDatabase Database =>
        new(
            new AlertRouter(
                _configuration,
                new SendingService(
                    _configuration,
                    _ledger,
                    _templates,
                    _mail,
                    _sms,
                    RestrictionKeySuppliers.None,
                    Considered.Nothing(_work, _clock),
                    new SmsBalance(_configuration, _sms, _balances, _work, _events, _clock),
                    _work,
                    _events,
                    _clock,
                    _randomness),
                _alerts,
                _work,
                _log),
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// INT-GEN-006 AC1: the resolver holds nothing it could reach a third party with,
    /// so no address a person signs in from can leave the deployment.
    /// </summary>
    [Fact]
    public void INT_GEN_006_AC1_TheResolverHoldsNothingItCouldCallOutWith()
    {
        ParameterInfo[] held = typeof(LocationDatabase)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single()
            .GetParameters();

        Assert.Equal(
            [typeof(AlertRouter), typeof(TimeProvider)],
            held.Select(parameter => parameter.ParameterType));
    }

    /// <summary>
    /// INT-GEN-006 AC3: with no file available the resolver answers no location.
    /// </summary>
    [Fact]
    public async Task INT_GEN_006_AC3_WithNoFileAvailableNoLocationIsAnsweredAsync()
    {
        SessionLocation? where = await Database.ResolveAsync(
            "198.51.100.7",
            TestContext.Current.CancellationToken);

        Assert.Null(where);
    }

    /// <summary>
    /// INT-GEN-006 AC2, OPS-ALERT-002 AC1: the missing file is a degradation the
    /// operator sees, once a window and not once a sign-in.
    /// </summary>
    [Fact]
    public async Task INT_GEN_006_AC2_TheMissingFileSurfacesAsOneDegradationAsync()
    {
        for (int session = 0; session < 3; session++)
        {
            _ = await Database.ResolveAsync(
                "198.51.100.7",
                TestContext.Current.CancellationToken);
        }

        MailMessage alert = Assert.Single(_mail.Taken);
        Assert.Equal("ops@example.test", alert.Destination.Value);
        Assert.Empty(_sms.Taken);
    }

    /// <summary>
    /// INT-GEN-006 AC3: an alert that cannot be carried is not a reason to refuse the
    /// session, which is recorded without a location either way.
    /// </summary>
    [Fact]
    public async Task INT_GEN_006_AC3_AnUndeliveredAlertStillAnswersNoLocationAsync()
    {
        _mail.Accepts = false;

        SessionLocation? where = await Database.ResolveAsync(
            "198.51.100.7",
            TestContext.Current.CancellationToken);

        Assert.Null(where);
    }
}
