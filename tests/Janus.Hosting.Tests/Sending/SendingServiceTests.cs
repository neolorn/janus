using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Configuration;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Sending;
using Xunit;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// The one path every message takes: what the named restrictions decide, what a
/// refusal says, what a send counts against, and what a transport that would not
/// take it leaves behind (AUTH-ABUSE-002, AUTH-ABUSE-004, AUTH-ABUSE-006,
/// INT-SMS-001, INT-SMS-004, INT-GEN-005, OPS-ALERT-003), what is considered about
/// a number before a restricted factor goes to it (AUTH-FACT-002b), the languages a
/// message goes out in (IDN-ATTR-001), and how a message no transport took is carried
/// again (D-022, INF-BG-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class SendingServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly EmailAddress Mailbox = Address("someone@example.test");

    private static readonly PhoneNumber Phone = Number("+201001234567");

    private static readonly string[] Declared = ["en", "ar"];

    private static readonly StepUpChallenge Satisfied =
        new(StepUpOutcome.Satisfied, AssuranceLevel.Aal2, PhishingResistant: false, [], null);

    private readonly ConfigurationInMemory _configuration = new();
    private readonly SendLedgerInMemory _ledger = new();
    private readonly SendOutboxInMemory _outbox = new();
    private readonly SendAuditInMemory _audit = new();
    private readonly MessageTemplatesInMemory _templates = new();
    private readonly MailTransportInMemory _mail = new();
    private readonly SmsTransportInMemory _sms = new();
    private readonly SmsBalanceLedgerInMemory _balances = new();
    private readonly PhoneSignalAuditInMemory _signals = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();

    private RestrictionKeySuppliers _suppliers = RestrictionKeySuppliers.None;

    private PhoneSignalProvider? _provider;

    /// <summary>
    /// A deployment that has named the one key with no default, administered by
    /// whoever edits it here.
    /// </summary>
    public SendingServiceTests()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

        var administrative = OrganizationId.New(_clock);
        _administrative.Organization = administrative;
        _gate.GrantEveryone(administrative, Permissions.SystemAdminister);
    }

    private SendingService Service =>
        new(
            _configuration,
            _ledger,
            _outbox,
            _templates,
            _mail,
            _sms,
            _suppliers,
            new PhoneSignals(_provider, _signals, _work, _clock),
            new SmsBalance(_configuration, _sms, _balances, _work, _events, _clock),
            _work,
            _events,
            _events,
            _clock,
            _randomness);

    private RestrictionAdministration Administration =>
        new(
            _configuration,
            new ConfigurationAdministration(
                _configuration,
                new ConfigurationAuditInMemory(),
                new AdministrativeScope(_gate, _administrative),
                new PolicyResolution(new MembershipLookupInMemory(), _configuration, new PolicyRaiseStoreInMemory()),
                new RelayRegistration(_configuration, _events, _clock),
                _events,
                _work,
                _clock),
            _ledger,
            _audit,
            RestrictionKeySuppliers.None,
            _work,
            _events,
            _events,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC1: a fourth text message to one number inside a day is
    /// refused, and the refusal says when the bucket lets the next one through.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC1_AFourthTextMessageInsideADayIsRefusedWithTheLiftAsync()
    {
        for (int sent = 0; sent < 3; sent++)
        {
            await SentAsync(Texted());
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        Result<SendReference> fourth = await Service.SendAsync(
            Texted(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(fourth));
        Assert.Equal(Noon + TimeSpan.FromHours(24), RetryAt(fourth));
        Assert.Equal(3, _sms.Taken.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC1: a second mail to one address inside sixty seconds is
    /// refused likewise, by the fixed bucket that turns over on the minute.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC1_ASecondMailInsideAMinuteIsRefusedWithTheLiftAsync()
    {
        await SentAsync(Mailed());

        _clock.Advance(TimeSpan.FromSeconds(10));

        Result<SendReference> second = await Service.SendAsync(
            Mailed(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(second));
        Assert.Equal(Noon + TimeSpan.FromSeconds(60), RetryAt(second));
        Assert.Single(_mail.Taken);
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: what the deployment can learn about the number is asked for
    /// before a restricted factor is carried to it, and the answer is written down
    /// against the entry it was asked for.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_TheSignalIsConsideredBeforeARestrictedFactorGoesAsync()
    {
        var subject = new SubjectId(Guid.NewGuid());
        var asked = new List<string>();

        _provider = new PhoneSignalProvider((number, _) =>
        {
            asked.Add(number);

            Assert.Empty(_sms.Taken);

            return ValueTask.FromResult(PhoneSignal.Risk);
        });

        _ = await SentAsync(Link(subject));

        Assert.Equal([Phone.Value], asked);
        Assert.Equal([(Factor.PhoneLink, PhoneSignal.Risk, subject)], _signals.Records);
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: where the deployment registered nothing to answer, the
    /// absence is what the record says and the send goes on.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AnAbsentProviderIsItselfRecordedAsync()
    {
        _ = await SentAsync(Link());

        Assert.Single(_sms.Taken);
        Assert.Equal([(Factor.PhoneLink, (PhoneSignal?)null, (SubjectId?)null)], _signals.Records);
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: nothing is asked about a number a message that is no factor
    /// goes to, and nothing about an address, so a verification code and a notice
    /// leave the provider alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_NothingIsConsideredForAMessageThatIsNoFactorAsync()
    {
        _provider = new PhoneSignalProvider((_, _) =>
            throw new Xunit.Sdk.XunitException("The provider was asked about a message that is no factor."));

        _ = await SentAsync(Texted());
        _ = await SentAsync(Mailed());

        Assert.Empty(_signals.Records);
    }

    /// <summary>
    /// AUTH-FACT-016 AC4: the code the new-device check sends is an ordinary mail to
    /// a destination, counted against the email destination restriction and refused
    /// with the restriction's own code once that destination is exhausted.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC4_TheCheckCodeCountsAgainstTheEmailDestinationAsync()
    {
        await SentAsync(Mailed());

        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", Mailbox.Value)));

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await Service.SendAsync(Mailed(), TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC2: a transport that would not take the message leaves
    /// nothing counted, so the next attempt is not held by the one that failed.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC2_ATransportRefusalCountsNothingAsync()
    {
        _sms.Accepts = false;

        Result<SendReference> refused = await Service.SendAsync(
            Texted(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SystemFault, Refusal(refused));
        Assert.Empty(_ledger.Keys);

        _sms.Accepts = true;

        await SentAsync(Texted());

        Assert.Single(_ledger.Sends(new RestrictionKey("sms.destination", Phone.Value)));
    }

    /// <summary>
    /// D-022: the message is written to the outbox in the transaction that undertook
    /// it, carried from there, and removed once a transport has taken it, so nothing
    /// outstanding is lost and nothing taken is kept (IDN-PRIN-003).
    /// </summary>
    [Fact]
    public async Task D_022_TheMessageIsWrittenToTheOutboxAndRemovedOnceTakenAsync()
    {
        await SentAsync(Mailed());

        SendDelivery written = Assert.Single(_outbox.Written);

        Assert.Equal(Noon, written.RecordedAt);
        Assert.Equal(Mailbox.Value, written.Requested.Destination.Canonical);
        Assert.Equal(MessageKind.VerificationCode, written.Requested.Message);
        Assert.Empty(_outbox.Waiting);
        Assert.Single(_mail.Taken);
    }

    /// <summary>
    /// D-022: a transport that would not take the message leaves it in the outbox with
    /// the attempt counted, which is what the publisher retries from; a refused
    /// delivery counts against no bucket (AUTH-ABUSE-004 AC2).
    /// </summary>
    [Fact]
    public async Task D_022_ATransportRefusalLeavesTheMessageRecordedAsync()
    {
        _mail.Accepts = false;

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refusal(await Service.SendAsync(Mailed(), TestContext.Current.CancellationToken)));

        SendDelivery waiting = Assert.Single(_outbox.Waiting);

        Assert.Equal(Mailbox.Value, waiting.Requested.Destination.Canonical);
        Assert.Equal(1, waiting.Attempts);
        Assert.Empty(_ledger.Keys);
    }

    /// <summary>
    /// D-022, INF-BG-001: a message no transport took is carried by the publisher once
    /// its next attempt is due, counted once it is taken, and removed.
    /// </summary>
    [Fact]
    public async Task D_022_ARefusedMessageIsCarriedOnceItsRetryIsDueAsync()
    {
        _mail.Accepts = false;

        _ = await Service.SendAsync(Mailed(), TestContext.Current.CancellationToken);

        _mail.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_mail.Taken);
        Assert.Empty(_outbox.Waiting);
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", Mailbox.Value)));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC1 and AC2: two mails a transport refused counted nothing, so
    /// the restrictions admitted both; carried again, they are judged again, and the
    /// second inside the minute waits rather than going out with the first.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC2_ARetryIsJudgedByTheRestrictionsAgainAsync()
    {
        _mail.Accepts = false;

        _ = await Service.SendAsync(Mailed(), TestContext.Current.CancellationToken);
        _ = await Service.SendAsync(Mailed(), TestContext.Current.CancellationToken);

        Assert.Equal(2, _outbox.Waiting.Count);

        _mail.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_mail.Taken);
        Assert.Equal(2, Assert.Single(_outbox.Waiting).Attempts);
    }

    /// <summary>
    /// IDN-ATTR-001, D-022: a retry carries only the languages no transport has taken,
    /// so the recipient is not sent again the one they already have.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_ARetryCarriesOnlyTheLanguagesStillOwedAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);
        _templates.Set(MessageKind.VerificationCode, SendKind.Email, "en", new MessageTemplate("code", "english"));
        _templates.Set(MessageKind.VerificationCode, SendKind.Email, "ar", new MessageTemplate("code", "arabic"));
        _mail.Takes = 1;

        _ = await Service.SendAsync(Mailed() with { Language = null }, TestContext.Current.CancellationToken);

        Assert.Equal(["en"], Assert.Single(_outbox.Waiting).Taken);

        // The language taken counted against the destination's one mail a minute.
        _mail.Takes = int.MaxValue;
        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(1, await RetriedAsync());
        Assert.Equal(["english", "arabic"], _mail.Taken.Select(mail => mail.Body));
        Assert.Empty(_outbox.Waiting);
        Assert.Equal(2, _ledger.Sends(new RestrictionKey("email.destination", Mailbox.Value)).Count);
    }

    /// <summary>
    /// D-022, INF-BG-001: a message still refused when <c>outbox.retry.maxattempts</c>
    /// is spent is removed and raises <c>degradation</c> for its channel, naming the
    /// message by its identifier and never by where it was going; nothing is counted.
    /// </summary>
    [Fact]
    public async Task D_022_AMessageWhoseBudgetIsSpentIsRemovedAndRaisesDegradationAsync()
    {
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 2);
        _mail.Accepts = false;

        _ = await Service.SendAsync(Mailed(), TestContext.Current.CancellationToken);

        SendDelivery waiting = Assert.Single(_outbox.Waiting);

        Assert.Empty(_events.Of<AlertRaised>());

        _clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_outbox.Waiting);
        Assert.Empty(_ledger.Keys);

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.Degradation, raised.Condition);
        Assert.StartsWith(
            Alerts.Key(AlertCondition.Degradation, "send:email") + "@",
            raised.IdempotencyKey,
            StringComparison.Ordinal);
        Assert.Equal(waiting.Id.ToString(), raised.Details["delivery"].GetString());
        Assert.Equal("email", raised.Details["channel"].GetString());
        Assert.Equal(2, raised.Details["attempts"].GetInt32());
        Assert.DoesNotContain(Mailbox.Value, JsonSerializer.Serialize(raised.Details), StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC4: credit granted to a key carries one send each and the key
    /// is refused again once it is spent.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC4_AGrantedKeyIsRefusedOnceTheCreditIsSpentAsync()
    {
        var key = new RestrictionKey("sms.destination", Phone.Value);

        _ledger.Given(key, Noon, Noon, Noon);
        await _ledger.GrantAsync(key, 1, TestContext.Current.CancellationToken);

        await SentAsync(Texted());

        Assert.Equal([key], _ledger.Spent);
        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await Service.SendAsync(Texted(), TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC5: a security notice to an address an account holds goes out
    /// with the destination restrictions exhausted, and answers to the one whose
    /// purpose names notifications.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC5_ANoticeToAHolderPassesADrainedDestinationAsync()
    {
        var subject = SubjectId.New(_randomness);

        _ledger.Given(
            new RestrictionKey("email.destination", Mailbox.Value),
            Noon,
            Noon,
            Noon,
            Noon,
            Noon);

        await SentAsync(Notice(subject));

        Assert.Single(_mail.Taken);

        _ledger.Given(
            new RestrictionKey("notification.destination", Mailbox.Value),
            Noon,
            Noon,
            Noon,
            Noon,
            Noon);

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await Service.SendAsync(Notice(subject), TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// INT-SMS-001 AC4: a security notice to a phone an account holds answers to the
    /// notification restriction and to no other, so draining the destination bucket
    /// does not silence the notice that says the account was touched.
    /// </summary>
    [Fact]
    public async Task INT_SMS_001_AC4_ANoticeToAHeldPhoneAnswersToNotificationOnlyAsync()
    {
        var subject = SubjectId.New(_randomness);

        _ledger.Given(new RestrictionKey("sms.destination", Phone.Value), Noon, Noon, Noon);

        await SentAsync(TextedNotice(subject));

        Assert.Single(_sms.Taken);

        _ledger.Given(
            new RestrictionKey("notification.destination", Phone.Value),
            Noon,
            Noon,
            Noon,
            Noon,
            Noon);

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await Service.SendAsync(TextedNotice(subject), TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC7: a restriction whose key the host supplies is evaluated
    /// exactly as a built-in key is.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC7_AHostSuppliedKeyIsEvaluatedLikeTheBuiltInOnesAsync()
    {
        _suppliers = RestrictionKeySuppliers.Of(
        [
            new RestrictionKeySupplier("tenant", (_, _) => ValueTask.FromResult("acme")),
        ]);

        _configuration.Set(
            Settings.Restrictions,
            [
                new Restriction(
                    "tenant.sends",
                    RestrictionKeyKind.Host,
                    "tenant",
                    RestrictionPurpose.Any,
                    [new Bucket(1, TimeSpan.FromHours(1), BucketWindow.Sliding)]),
            ]);

        await SentAsync(Texted());

        Assert.Equal([new RestrictionKey("tenant.sends", "acme")], _ledger.Keys);

        Result<SendReference> second = await Service.SendAsync(
            Texted(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(second));
        Assert.Equal(Noon + TimeSpan.FromHours(1), RetryAt(second));
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC3: the refusal a restriction produces is the same for an
    /// address an account holds and one it does not, and carries <c>retryAt</c>.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC3_ARefusedSendAnswersTheSameForEitherAddressAsync()
    {
        _ledger.Given(new RestrictionKey("sms.destination", Phone.Value), Noon, Noon, Noon);

        Result<SendReference> unregistered = await Service.SendAsync(
            Texted(),
            TestContext.Current.CancellationToken);

        Result<SendReference> registered = await Service.SendAsync(
            Texted() with { Subject = SubjectId.New(_randomness) },
            TestContext.Current.CancellationToken);

        Assert.Equal(Refusal(unregistered), Refusal(registered));
        Assert.Equal(RetryAt(unregistered), RetryAt(registered));
        Assert.Equal(Written(unregistered), Written(registered));
    }

    /// <summary>
    /// INT-SMS-004 AC2 and AUTH-ABUSE-006 AC2: an ordinary text message is refused
    /// below the floor, and the refusal names the condition.
    /// </summary>
    [Fact]
    public async Task INT_SMS_004_AC2_AnOrdinarySendIsRefusedBelowTheFloorAsync()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 50m);
        _sms.Balance = 40m;

        Assert.Equal(
            ErrorCodes.SmsBalanceFloor,
            Refusal(await Service.SendAsync(Texted(), TestContext.Current.CancellationToken)));

        Assert.Empty(_sms.Taken);
    }

    /// <summary>
    /// INT-SMS-004 AC2: a text message carried again is held below the floor as any
    /// ordinary send is, and the attempt counts against its budget.
    /// </summary>
    [Fact]
    public async Task INT_SMS_004_AC2_ARetryIsHeldBelowTheFloorAsync()
    {
        _sms.Accepts = false;

        _ = await Service.SendAsync(Texted(), TestContext.Current.CancellationToken);

        _sms.Accepts = true;
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 50m);
        _sms.Balance = 40m;
        _clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_sms.Taken);
        Assert.Equal(2, Assert.Single(_outbox.Waiting).Attempts);
    }

    /// <summary>
    /// OPS-ALERT-003 AC3: an alert-class send continues below the floor, because a
    /// drained account must not be able to silence the alerting.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_003_AC3_AnAlertIsSentBelowTheFloorAsync()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 50m);
        _sms.Balance = 0m;

        await SentAsync(new SendRequest(
            SendDestination.Of(Phone),
            MessageKind.Alert,
            RestrictionPurpose.Notification,
            "operator",
            "en"));

        Assert.Single(_sms.Taken);
    }

    /// <summary>
    /// OPS-ALERT-003: an alert is outside the destination restrictions as well, so
    /// an exhausted operator number is not a silenced channel.
    /// </summary>
    [Fact]
    public async Task SendAsync_AnAlertToAnExhaustedNumber_IsSentAsync()
    {
        _ledger.Given(new RestrictionKey("sms.destination", Phone.Value), Noon, Noon, Noon);

        await SentAsync(new SendRequest(
            SendDestination.Of(Phone),
            MessageKind.Alert,
            RestrictionPurpose.Notification,
            "operator",
            "en"));

        Assert.Single(_sms.Taken);
    }

    /// <summary>
    /// INT-GEN-005 AC1: the field set of an outbound mail is the destination, the
    /// subject, the body and the correlation reference, and nothing else.
    /// </summary>
    [Fact]
    public async Task INT_GEN_005_AC1_AnOutboundMailCarriesItsExactFieldSetAsync()
    {
        _templates.Set(
            MessageKind.VerificationCode,
            SendKind.Email,
            "en",
            new MessageTemplate("your code", "the code is {code}"));

        SendReference reference = await SentAsync(Mailed() with
        {
            Values = new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = "429184" },
        });

        MailMessage carried = Assert.Single(_mail.Taken);

        Assert.Equal(
            new MailMessage(Mailbox, "your code", "the code is 429184", reference.Value),
            carried);
    }

    /// <summary>
    /// INT-GEN-005 AC1: the field set of an outbound text message is the
    /// destination, the text and the correlation reference, and nothing else.
    /// </summary>
    [Fact]
    public async Task INT_GEN_005_AC1_AnOutboundTextMessageCarriesItsExactFieldSetAsync()
    {
        _templates.Set(
            MessageKind.VerificationCode,
            SendKind.Sms,
            "en",
            new MessageTemplate(null, "code {code}"));

        SendReference reference = await SentAsync(Texted() with
        {
            Values = new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = "429184" },
        });

        SmsMessage carried = Assert.Single(_sms.Taken);

        Assert.Equal(new SmsMessage(Phone, "code 429184", reference.Value), carried);
    }

    /// <summary>
    /// IDN-ATTR-001 AC3: a message to someone whose language nothing names goes out
    /// in every language the deployment declares, each in the deployment's own
    /// template for it. The restrictions judge the request once, and each language
    /// carried is a message of its own (AUTH-ABUSE-004 AC1): its own reference, its
    /// own announcement and its own count, so the next mail inside the minute is
    /// refused and a report of one failed delivery releases that one alone.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_AC3_NoKnownLanguageGoesOutInEveryDeclaredOneAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);
        _templates.Set(MessageKind.VerificationCode, SendKind.Email, "en", new MessageTemplate("code", "english"));
        _templates.Set(MessageKind.VerificationCode, SendKind.Email, "ar", new MessageTemplate("code", "arabic"));
        var destination = new RestrictionKey("email.destination", Mailbox.Value);

        SendReference reference = await SentAsync(Mailed() with { Language = null });

        Assert.Equal(["english", "arabic"], _mail.Taken.Select(mail => mail.Body));
        Assert.Equal(reference.Value, _mail.Taken[0].Reference);
        Assert.NotEqual(_mail.Taken[0].Reference, _mail.Taken[1].Reference);
        Assert.Equal(
            _mail.Taken.Select(mail => mail.Reference),
            _events.Of<NotificationRequested>().Select(announced => announced.IdempotencyKey));
        Assert.Equal([Noon, Noon], _ledger.Sends(destination));
        Assert.Empty(_outbox.Waiting);

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await Service.SendAsync(Mailed(), TestContext.Current.CancellationToken)));

        Assert.True(
            await _ledger.ReleaseAsync(
                SendReferences.Of(_mail.Taken[1].Reference),
                TestContext.Current.CancellationToken));
        Assert.Single(_ledger.Sends(destination));
    }

    /// <summary>
    /// IDN-ATTR-001 and AUTH-ABUSE-004 AC2: where a transport takes one language and
    /// refuses the next, what it took counts, what it refused does not, and the
    /// message stays recorded for the retry.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_ALanguageTheTransportRefusedLeavesTheMessageRecordedAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);
        _templates.Set(MessageKind.VerificationCode, SendKind.Email, "en", new MessageTemplate("code", "english"));
        _templates.Set(MessageKind.VerificationCode, SendKind.Email, "ar", new MessageTemplate("code", "arabic"));
        _mail.Takes = 1;

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refusal(await Service.SendAsync(Mailed() with { Language = null }, TestContext.Current.CancellationToken)));

        Assert.Equal("english", Assert.Single(_mail.Taken).Body);
        Assert.Single(_outbox.Waiting);
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", Mailbox.Value)));
    }

    /// <summary>
    /// IDN-ATTR-001: a message whose language is known goes out in that language
    /// alone.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_AKnownLanguageIsTheOnlyOneSentAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);
        _templates.Set(MessageKind.VerificationCode, SendKind.Email, "ar", new MessageTemplate("code", "arabic"));

        _ = await SentAsync(Mailed() with { Language = "ar" });

        Assert.Equal("arabic", Assert.Single(_mail.Taken).Body);
    }

    /// <summary>
    /// A send the transport took is announced, so the outbox and any host consumer
    /// see one event per message (chapter 10 section 5b).
    /// </summary>
    [Fact]
    public async Task SendAsync_AMessageTheTransportTook_IsAnnouncedOnceAsync()
    {
        SendReference reference = await SentAsync(Texted());

        NotificationRequested announced = Assert.Single(_events.Of<NotificationRequested>());

        Assert.Equal(reference.Value, announced.IdempotencyKey);
        Assert.Equal(MessageKind.VerificationCode, announced.Message);
        Assert.Equal(SendKind.Sms, announced.Kind);
    }

    /// <summary>
    /// A restriction naming a host key no supplier answers for stops the send rather
    /// than letting it through unevaluated (LIB-HOST-001).
    /// </summary>
    [Fact]
    public async Task SendAsync_ARestrictionWithNoSupplier_IsRefusedAsync()
    {
        _configuration.Set(
            Settings.Restrictions,
            [
                new Restriction(
                    "tenant.sends",
                    RestrictionKeyKind.Host,
                    "tenant",
                    RestrictionPurpose.Any,
                    [new Bucket(1, TimeSpan.FromHours(1), BucketWindow.Sliding)]),
            ]);

        Assert.Equal(
            ErrorCodes.StartupDeclarationMissing,
            Refusal(await Service.SendAsync(Texted(), TestContext.Current.CancellationToken)));

        Assert.Empty(_sms.Taken);
    }

    // What one member takes and gives: the types a send could be told a preference
    // through.
    /// <summary>
    /// AUTH-ABUSE-004 AC6: what a record is kept for is the longest interval the
    /// restrictions now declare, so the read before a send takes with it every record
    /// older than that, including the records of keys this send never names.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC6_ARecordOlderThanTheLongestIntervalGoesWithTheNextReadAsync()
    {
        var untouched = new RestrictionKey("sms.destination", "+201009999999");

        _ledger.Given(untouched, Noon - TimeSpan.FromHours(25));

        Assert.Contains(untouched, _ledger.Keys);

        await SentAsync(Texted());

        Assert.DoesNotContain(untouched, _ledger.Keys);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC3: an edit that goes through applies to the very next send,
    /// with nothing restarted in between.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC3_AnEditAppliesToTheNextSendAsync()
    {
        await SentAsync(Texted());

        (await Administration.EditAsync(
            "sms.destination",
            new Restriction(
                "sms.destination",
                RestrictionKeyKind.Destination,
                null,
                RestrictionPurpose.Any,
                [new Bucket(1, TimeSpan.FromHours(24), BucketWindow.Sliding)]),
            "an incident",
            Satisfied,
            SubjectId.New(_randomness),
            TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The edit was refused: {error.Code}."));

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await Service.SendAsync(Texted(), TestContext.Current.CancellationToken)));
    }

    private static IEnumerable<Type> Carried(MemberInfo member) =>
        member switch
        {
            MethodBase method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append((method as MethodInfo)?.ReturnType ?? typeof(void)),
            PropertyInfo property => [property.PropertyType],
            FieldInfo field => [field.FieldType],
            _ => [],
        };

    private static EmailAddress Address(string entered) =>
        EmailAddress.TryParse(entered, out EmailAddress address)
            ? address
            : throw new Xunit.Sdk.XunitException("The address does not parse.");

    private static PhoneNumber Number(string entered) =>
        PhoneNumber.TryParse(entered, out PhoneNumber number)
            ? number
            : throw new Xunit.Sdk.XunitException("The number does not parse.");

    private static SendRequest Texted() =>
        new(
            SendDestination.Of(Phone),
            MessageKind.VerificationCode,
            RestrictionPurpose.Verification,
            "198.51.100.7",
            "en");

    private static SendRequest Link(SubjectId? subject = null) =>
        new(
            SendDestination.Of(Phone),
            MessageKind.SignInLink,
            RestrictionPurpose.SignIn,
            "198.51.100.7",
            "en")
        {
            Subject = subject,
        };

    private static SendRequest Mailed() =>
        new(
            SendDestination.Of(Mailbox),
            MessageKind.VerificationCode,
            RestrictionPurpose.Verification,
            "198.51.100.7",
            "en");

    private static SendRequest Notice(SubjectId subject) =>
        new(
            SendDestination.Of(Mailbox),
            MessageKind.SecurityNotice,
            RestrictionPurpose.Notification,
            "198.51.100.7",
            "en")
        {
            Subject = subject,
        };

    private static SendRequest TextedNotice(SubjectId subject) =>
        new(
            SendDestination.Of(Phone),
            MessageKind.SecurityNotice,
            RestrictionPurpose.Notification,
            "198.51.100.7",
            "en")
        {
            Subject = subject,
        };

    private static ErrorCode Refusal<TValue>(Result<TValue> result) =>
        result.Match(
            _ => throw new Xunit.Sdk.XunitException("The send was not refused."),
            error => error.Code);

    private static IReadOnlyDictionary<string, JsonElement> Details<TValue>(Result<TValue> result) =>
        result.Match(
            _ => throw new Xunit.Sdk.XunitException("The send was not refused."),
            error => error.Details);

    // JsonElement carries no value equality, so two refusals are compared as the
    // bytes the error envelope would carry.
    private static string Written<TValue>(Result<TValue> result) =>
        JsonSerializer.Serialize(Details(result));

    private static DateTimeOffset RetryAt<TValue>(Result<TValue> result) =>
        DateTimeOffset.Parse(
            Details(result)["retryAt"].GetString()
            ?? throw new Xunit.Sdk.XunitException("The refusal carries no retryAt."),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    private async Task<int> RetriedAsync() =>
        (await Service.RetryAsync(TestContext.Current.CancellationToken)).Match(
            carried => carried,
            error => throw new Xunit.Sdk.XunitException($"The pass failed: {error.Code}."));

    private async Task<SendReference> SentAsync(SendRequest request) =>
        (await Service.SendAsync(request, TestContext.Current.CancellationToken)).Match(
            reference => reference,
            error => throw new Xunit.Sdk.XunitException($"The send was refused: {error.Code}."));
}
