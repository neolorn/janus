using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Alerting;
using Janus.Authentication.Tests.Configuration;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Alerting;
using Janus.Hosting.Sending;
using Xunit;

namespace Janus.Hosting.Tests.Alerting;

/// <summary>
/// Moving the alerting somewhere else, which is the one change that could blind a
/// deployment to the person making it: the destinations being replaced are told
/// first, the notice answers to no setting, and a channel cannot be emptied
/// (OPS-ALERT-004a).
/// </summary>
[Trait("kind", "unit")]
public sealed class AlertDestinationChangeTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly StepUpChallenge Satisfied =
        new(StepUpOutcome.Satisfied, AssuranceLevel.Aal2, PhishingResistant: false, [], null);

    private static readonly StepUpChallenge Wanting =
        new(StepUpOutcome.Present, AssuranceLevel.Aal2, PhishingResistant: false, [[Factor.Totp]], null);

    private static readonly string[] OneLanguage = ["en"];

    private static readonly string[] ThreeAddresses =
        ["first@example.test", "ops@example.test", "second@example.test"];

    private static readonly string[] TwoNumbers = ["+201001234567", "+201007654321"];

    private static readonly string[] Elsewhere = ["elsewhere@example.test"];

    private static readonly string[] Nowhere = [];

    private readonly ConfigurationInMemory _configuration = new();
    private readonly SendLedgerInMemory _ledger = new();
    private readonly AlertLedgerInMemory _alerts = new();
    private readonly AlertLogInMemory _log = new();
    private readonly MessageTemplatesInMemory _templates = new();
    private readonly MailTransportInMemory _mail = new();
    private readonly SmsTransportInMemory _sms = new();
    private readonly SmsBalanceLedgerInMemory _balances = new();
    private readonly ConfigurationAuditInMemory _changes = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();

    /// <summary>
    /// A deployment alerting three addresses and two numbers, administered by whoever
    /// changes them here.
    /// </summary>
    public AlertDestinationChangeTests()
    {
        _configuration.Set(Settings.NotificationLanguages, OneLanguage);
        _configuration.Set(Settings.AlertingEmailDestinations, ThreeAddresses);
        _configuration.Set(Settings.AlertingSmsDestinations, TwoNumbers);
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 100m);
        _configuration.Set(Settings.AlertingOwnerEmail, "owner@example.test");
        _configuration.Set(Settings.AlertingOwnerSms, "+201009999999");
        _sms.Balance = 1000m;

        var administrative = OrganizationId.New(_clock);
        _administrative.Organization = administrative;
        _gate.GrantEveryone(administrative, Permissions.SystemAdminister);
    }

    private AlertDestinationChange Change =>
        new(
            _configuration,
            new ConfigurationAdministration(
                _configuration,
                _changes,
                new AdministrativeScope(_gate, _administrative),
                new PolicyResolution(new MembershipLookupInMemory(), _configuration, new PolicyRaiseStoreInMemory()),
                _work,
                _clock),
            new AlertRouter(
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
                    _clock,
                    _randomness),
                _alerts,
                _work,
                _log),
            _events,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// OPS-ALERT-004a AC1: every one of the destinations being replaced is told, and
    /// the replacement is what the deployment holds afterwards.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_004a_AC1_EveryPreviousDestinationIsNotifiedAsync()
    {
        await ChangedAsync(SendKind.Email, Elsewhere);

        Assert.Equal(
            ThreeAddresses.Order(StringComparer.Ordinal),
            _mail.Taken.Select(mail => mail.Destination.Value).Order(StringComparer.Ordinal));

        Assert.Equal(Elsewhere, await DestinationsAsync(Settings.AlertingEmailDestinations));
    }

    /// <summary>
    /// OPS-ALERT-004a AC4: the notice reaches the destinations being replaced and
    /// not the ones replacing them, which is the whole of the requirement.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_004a_AC4_TheNoticeReachesThePreviousDestinationsAsync()
    {
        await ChangedAsync(SendKind.Email, Elsewhere);

        Assert.DoesNotContain(
            _mail.Taken,
            mail => string.Equals(mail.Destination.Value, Elsewhere[0], StringComparison.Ordinal));

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.AlertDestinationChanged, raised.Condition);
        Assert.Equal(AlertSeverity.High, raised.Severity);
        Assert.Equal("alerting.email.destinations", raised.Details["key"].GetString());
        Assert.Equal(3, raised.Details["destinationsBefore"].GetInt32());
        Assert.Equal(1, raised.Details["destinationsAfter"].GetInt32());
    }

    /// <summary>
    /// OPS-ALERT-004a AC2: no setting reaches the notice. There is no key for it, and
    /// two changes inside one deduplication window are two notices, because the one
    /// bound on alert volume does not apply to this one.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_004a_AC2_TheNoticeAnswersToNoSettingAsync()
    {
        Assert.DoesNotContain(
            Settings.All,
            setting => setting.Key.ToString().Contains("destinationchange", StringComparison.Ordinal));

        _configuration.Set(Settings.AlertingDedupeWindow, TimeSpan.FromDays(30));
        _configuration.Set(Settings.AlertingOwnerEnabled, false);

        await ChangedAsync(SendKind.Email, Elsewhere);
        await ChangedAsync(SendKind.Email, ThreeAddresses);

        Assert.Equal(4, _mail.Taken.Count);
        Assert.Equal(Elsewhere[0], _mail.Taken[^1].Destination.Value);
    }

    /// <summary>
    /// OPS-ALERT-004a AC3: the last destination of a channel cannot be removed, so
    /// there is no change that leaves a channel carrying nothing.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_004a_AC3_TheLastDestinationCannotBeRemovedAsync()
    {
        Error refusal = await RefusedAsync(SendKind.Email, Nowhere);

        Assert.Equal(ErrorCodes.ConfigurationLastDestination, refusal.Code);
        Assert.Equal(ThreeAddresses, await DestinationsAsync(Settings.AlertingEmailDestinations));
        Assert.Empty(_mail.Taken);
    }

    /// <summary>
    /// OPS-ALERT-004a AC5: a change that would leave a channel's list empty is
    /// refused with the code chapter 10 gives it, naming the key.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_004a_AC5_AnEmptyListIsRefusedWithTheNamedCodeAsync()
    {
        Error refusal = await RefusedAsync(SendKind.Sms, Nowhere);

        Assert.Equal("config.value.lastdestination", refusal.Code.ToString());
        Assert.Equal("alerting.sms.destinations", refusal.Details["key"].GetString());
        Assert.Equal(TwoNumbers, await DestinationsAsync(Settings.AlertingSmsDestinations));
    }

    /// <summary>
    /// Changing the numbers tells the numbers being replaced, on their own channel
    /// (OPS-ALERT-004a).
    /// </summary>
    [Fact]
    public async Task ChangeAsync_TheSmsChannel_TellsTheNumbersBeingReplacedAsync()
    {
        await ChangedAsync(SendKind.Sms, ["+201008888888"]);

        Assert.Equal(
            TwoNumbers.Order(StringComparer.Ordinal),
            _sms.Taken.Select(message => message.Destination.Value).Order(StringComparer.Ordinal));

        Assert.Empty(_mail.Taken);
    }

    /// <summary>
    /// A notice addressed to one channel names no destination on the other, so the
    /// other is not written down as having carried nothing (OPS-ALERT-003 AC4).
    /// </summary>
    [Fact]
    public async Task ChangeAsync_ANoticeToOneChannel_RecordsNothingUnreachableAsync()
    {
        await ChangedAsync(SendKind.Email, Elsewhere);

        Assert.Empty(_log.Unreached);
        Assert.Empty(_sms.Taken);
    }

    /// <summary>
    /// The change is behind the <c>alerting:destinations</c> gate, and a session the
    /// gate is not satisfied by changes nothing and tells nobody (OPS-ALERT-004a).
    /// </summary>
    [Fact]
    public async Task ChangeAsync_AGateTheSessionDoesNotMeet_ChangesNothingAsync()
    {
        Error refusal = await RefusedAsync(SendKind.Email, Elsewhere, Wanting);

        Assert.Equal(ErrorCodes.StepUpRequired, refusal.Code);
        Assert.Equal("alerting:destinations", refusal.Details["action"].GetString());
        Assert.Equal(ThreeAddresses, await DestinationsAsync(Settings.AlertingEmailDestinations));
        Assert.Empty(_mail.Taken);
        Assert.Empty(_events.Published);
    }

    /// <summary>
    /// OPS-CFG-002, OPS-CFG-005: the destination list is a runtime setting, so the
    /// change goes through the one operation and is written down with what it replaced.
    /// </summary>
    [Fact]
    public async Task ChangeAsync_ADestinationChange_IsWrittenDownAsARuntimeChangeAsync()
    {
        await ChangedAsync(SendKind.Email, Elsewhere);

        ConfigurationChange written = Assert.Single(_changes.Written);

        Assert.Equal(Settings.AlertingEmailDestinations.Key, written.Key);
        Assert.Equal(Settings.AlertingEmailDestinations.Write(ThreeAddresses), written.Before);
        Assert.Equal(Settings.AlertingEmailDestinations.Write(Elsewhere), written.After);
        Assert.True(written.Loosening);
        Assert.Equal("an incident", written.Reason);
    }

    /// <summary>
    /// OPS-CFG-002 AC3: the key has no direction, so the change carries a written
    /// reason. Without one it is refused before the destinations being replaced are
    /// told, because a notice of a change that did not happen is a false one.
    /// </summary>
    [Fact]
    public async Task ChangeAsync_ADestinationChangeWithNoReason_TellsNobodyAsync()
    {
        Error refusal = await RefusedAsync(SendKind.Email, Elsewhere, reason: null);

        Assert.Equal(ErrorCodes.RestrictionReasonRequired, refusal.Code);
        Assert.Equal("alerting.email.destinations", refusal.Details["key"].GetString());
        Assert.Equal(ThreeAddresses, await DestinationsAsync(Settings.AlertingEmailDestinations));
        Assert.Empty(_changes.Written);
        Assert.Empty(_mail.Taken);
        Assert.Empty(_events.Published);
    }

    private async Task<IReadOnlyList<string>> DestinationsAsync(TextListSetting setting) =>
        (await _configuration.ReadAsync(setting, TestContext.Current.CancellationToken)).Match(
            destinations => destinations,
            error => throw new Xunit.Sdk.XunitException($"The destinations were refused: {error.Code}."));

    private async Task ChangedAsync(SendKind channel, IReadOnlyList<string> replacement) =>
        (await Change.ChangeAsync(
            channel,
            replacement,
            "an incident",
            Satisfied,
            SubjectId.New(_randomness),
            TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The change was refused: {error.Code}."));

    private async Task<Error> RefusedAsync(
        SendKind channel,
        IReadOnlyList<string> replacement,
        StepUpChallenge? challenge = null,
        string? reason = "an incident") =>
        (await Change.ChangeAsync(
            channel,
            replacement,
            reason,
            challenge ?? Satisfied,
            SubjectId.New(_randomness),
            TestContext.Current.CancellationToken)).Match(
            () => throw new Xunit.Sdk.XunitException("The change was not refused."),
            error => error);
}
