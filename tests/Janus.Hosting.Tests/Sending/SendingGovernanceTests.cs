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
public sealed class SendingGovernanceTests : IAsyncDisposable
{
    private static readonly AccessContext Carrier = AccessContext.Of(
        SystemPrincipal.ForDeployment("sends", "INF-BG-001", SystemOperation.Delivery));

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly EmailAddress Mailbox = Address("someone@example.test");

    private static readonly PhoneNumber Phone = Number("+201001234567");

    private static readonly string[] Declared = ["en", "ar"];

    private static readonly TimeSpan Recency = TimeSpan.FromMinutes(15);

    private static readonly StepUpChallenge Satisfied =
        new(StepUpOutcome.Satisfied, AssuranceLevel.Aal2, PhishingResistant: false, Recency, [], null);

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

    private INotificationHandler? _replaced;

    /// <summary>
    /// A deployment that has named the one key with no default, administered by
    /// whoever edits it here.
    /// </summary>
    public SendingGovernanceTests()
    {
        _ledger.Work = _work;
        _outbox.Work = _work;
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

        var administrative = OrganizationId.New(_clock);
        _administrative.Organization = administrative;
        _gate.GrantEveryone(administrative, Permissions.SystemAdminister);
    }

    private SendingPath Path =>
        new(_configuration, _ledger, _outbox, _templates, _mail, _sms, _balances, _work, _events, _clock, _randomness)
        {
            Suppliers = _suppliers,
            Signals = new PhoneSignals(_provider, _signals, _work, _clock),
            Replaced = _replaced,
            Alerts = _events,
        };

    private RestrictionAdministration Administration =>
        new(
            _configuration,
            new ConfigurationAdministration(
                _configuration,
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

        Result<SendReference> fourth = await SendAsync(
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

        Result<SendReference> second = await SendAsync(
            Mailed(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(second));
        Assert.Equal(Noon + TimeSpan.FromSeconds(60), RetryAt(second));
        Assert.Single(_mail.Taken);
    }

    /// <summary>
    /// AUTH-ABUSE-004: a restriction governs only the sends on its channel, so a mail
    /// passes a spent <c>sms.destination</c> and a text message a spent
    /// <c>email.destination</c>, and each is counted under its own channel's restriction
    /// alone (D-166, 342).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_ARestrictionGovernsOnlyItsChannelAsync()
    {
        var textedMailbox = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Mailbox.Value);
        var mailedPhone = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Phone.Value);

        _ledger.Given(textedMailbox, Noon, Noon, Noon);
        _ledger.Given(mailedPhone, Noon);

        _clock.Advance(TimeSpan.FromSeconds(10));

        _ = await SentAsync(Mailed());
        _ = await SentAsync(Texted());

        Assert.Single(_mail.Taken);
        Assert.Single(_sms.Taken);
        Assert.Equal(3, _ledger.Sends(textedMailbox).Count);
        Assert.Single(_ledger.Sends(mailedPhone));
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));
        Assert.Single(_ledger.Sends(new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value)));
        Assert.Single(_ledger.Sends(new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.7")));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC5: a security notice to a number its owner holds answers to the
    /// restrictions whose purpose is <c>notification</c> alone, so a spent
    /// <c>sms.source</c> neither refuses it nor counts it (D-166, 342).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC5_ANoticeToAHolderIsNotCountedBySmsSourceAsync()
    {
        var subject = SubjectId.New(_randomness);
        var source = new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.7");
        DateTimeOffset[] spent = [.. Enumerable.Repeat(Noon, 10)];

        _ledger.Given(source, spent);

        _ = await SentAsync(TextedNotice(subject));

        Assert.Single(_sms.Taken);
        Assert.Equal(10, _ledger.Sends(source).Count);
        Assert.Single(_ledger.Sends(new RestrictionKey("notification.destination", RestrictionKeyKind.Destination, Phone.Value)));
    }

    /// <summary>
    /// AUTH-ABUSE-004 and chapter 10 section 5.14: a send no request asked for carries
    /// no source, and no <c>source</c> restriction counts it, however full a source's
    /// bucket stands (D-166, 342).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_ASendNoRequestAskedForIsCountedUnderNoSourceAsync()
    {
        _ = await SentAsync(Texted() with { Source = null });

        Assert.Single(_sms.Taken);
        Assert.DoesNotContain(_ledger.Keys, key => key.Restriction == "sms.source");
        Assert.Equal([new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value)], _ledger.Keys);
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
    /// AUTH-FACT-002b AC6: a recovery link texted to a number is considered as a sign-in
    /// link by text is, and the outcome is written down against the entry it amounts to.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_ARecoveryLinkByTextIsConsideredBeforeItGoesAsync()
    {
        var subject = new SubjectId(Guid.NewGuid());

        _provider = new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Clear));

        _ = await SentAsync(Link(subject) with
        {
            Message = MessageKind.RecoveryLink,
            Purpose = RestrictionPurpose.Notification,
        });

        Assert.Equal([(Factor.PhoneLink, (PhoneSignal?)PhoneSignal.Clear, (SubjectId?)subject)], _signals.Records);
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

        Assert.Equal(1, await RetriedAsync());
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

        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await SendAsync(Mailed(), TestContext.Current.CancellationToken)));
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
    /// D-022, AUTH-ABUSE-004 AC16: a handler that would not take the message leaves it
    /// in the outbox with the attempt counted, which is what the publisher retries from.
    /// The send was admitted, so it is answered with its reference and counts from its
    /// admission, whatever became of the attempt.
    /// </summary>
    [Fact]
    public async Task D_022_ATransportRefusalLeavesTheMessageRecordedAsync()
    {
        _mail.Accepts = false;

        SendReference reference = await SentAsync(Mailed());

        SendDelivery waiting = Assert.Single(_outbox.Waiting);

        Assert.Equal(Mailbox.Value, waiting.Requested.Destination.Canonical);
        Assert.Equal(reference.Value, waiting.Reference.Value);
        Assert.Equal(1, waiting.Attempts);
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));
    }

    /// <summary>
    /// D-022, INF-BG-001, AUTH-ABUSE-004 AC9: a message no handler took is carried by the
    /// publisher once its next attempt is due and removed. It is judged again with its
    /// own count set aside, so it counts once, at the instant of the retry.
    /// </summary>
    [Fact]
    public async Task D_022_ARefusedMessageIsCarriedOnceItsRetryIsDueAsync()
    {
        _mail.Accepts = false;

        _ = await SendAsync(Mailed(), TestContext.Current.CancellationToken);

        _mail.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_mail.Taken);
        Assert.Empty(_outbox.Waiting);
        Assert.Equal(
            [Noon + TimeSpan.FromSeconds(30)],
            _ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC9, AC16: a retried send is judged by the restrictions as they
    /// stand when it is retried, with its own count set aside. One they refuse is not
    /// handed to the handler, holds no count, and waits as any failed attempt does.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC9_ARetryIsJudgedByTheRestrictionsAgainAsync()
    {
        var key = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value);

        _mail.Accepts = false;

        _ = await SentAsync(Mailed());

        Assert.Single(_ledger.Sends(key));

        // The deployment now allows one mail an hour, and another took the hour's one.
        _configuration.Set<IReadOnlyList<Restriction>>(
            Settings.Restrictions,
            [
                new Restriction(
                    "email.destination",
                    RestrictionKeyKind.Destination,
                    HostKeyName: null,
                    RestrictionPurpose.Any,
                    [new Bucket(1, TimeSpan.FromHours(1), BucketWindow.Sliding)])
                {
                    Channel = RestrictionChannel.Email,
                },
            ]);
        _mail.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));
        _ledger.Given(key, Noon, Noon + TimeSpan.FromSeconds(20));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_mail.Taken);
        Assert.Equal(2, Assert.Single(_outbox.Waiting).Attempts);
        Assert.Equal([Noon + TimeSpan.FromSeconds(20)], _ledger.Sends(key));

        _clock.Advance(TimeSpan.FromHours(2));

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_mail.Taken);
        Assert.Equal([Noon + TimeSpan.FromSeconds(30) + TimeSpan.FromHours(2)], _ledger.Sends(key));
    }

    /// <summary>
    /// IDN-ATTR-001, D-022: a text message owed in every declared language is one
    /// message for each of them, each with a row of its own, so a retry carries only the
    /// languages no transport has taken and the recipient is not sent again the one they
    /// already have.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_ARetryCarriesOnlyTheLanguagesStillOwedAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);
        _templates.Set(MessageKind.VerificationCode, SendKind.Sms, "en", new MessageTemplate(null, "english"));
        _templates.Set(MessageKind.VerificationCode, SendKind.Sms, "ar", new MessageTemplate(null, "arabic"));
        _sms.Takes = 1;

        _ = await SendAsync(Texted() with { Language = null }, TestContext.Current.CancellationToken);

        Assert.Equal("ar", Assert.Single(_outbox.Waiting).Requested.Language);

        _sms.Takes = int.MaxValue;
        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(1, await RetriedAsync());
        Assert.Equal(["english", "arabic"], _sms.Taken.Select(message => message.Text));
        Assert.Empty(_outbox.Waiting);
        Assert.Equal(2, _ledger.Sends(new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value)).Count);
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

        _ = await SendAsync(Mailed(), TestContext.Current.CancellationToken);

        SendDelivery waiting = Assert.Single(_outbox.Waiting);

        Assert.Empty(_events.Of<AlertRaised>());

        _clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_outbox.Waiting);
        Assert.Empty(_ledger.Keys);

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.Degradation, raised.Condition);
        Assert.StartsWith(
            Alerts.Key(AlertCondition.Degradation, "send:email", named: null) + "@",
            raised.IdempotencyKey,
            StringComparison.Ordinal);
        Assert.Equal(waiting.Id.ToString(), raised.Details["delivery"].GetString());
        Assert.Equal("email", raised.Details["channel"].GetString());
        Assert.Equal(2, raised.Details["attempts"].GetInt32());
        Assert.DoesNotContain(Mailbox.Value, JsonSerializer.Serialize(raised.Details), StringComparison.Ordinal);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, D-022: a message whose attempts are spent and whose alert
    /// cannot be raised is refused after the unit of work that settles it began, and
    /// rolls it back, so the message is not removed, and its count not released, without
    /// its alert.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ASpentBudgetWhoseAlertCannotBeRaisedRollsBackAsync()
    {
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 2);
        _mail.Accepts = false;

        _ = await SendAsync(Mailed(), TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromHours(1));
        _events.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        Result<int> retried = await Path.Publisher.RetryAsync(Carrier, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SystemFault, Refusal(retried));
        Assert.False(_work.Open);
        Assert.Equal(1, _work.RolledBack);
        Assert.Equal(_work.Opened, _work.Committed + _work.RolledBack);
        Assert.Single(_outbox.Waiting);
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC4: credit granted to a key carries one send each and the key
    /// is refused again once it is spent.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC4_AGrantedKeyIsRefusedOnceTheCreditIsSpentAsync()
    {
        var key = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value);

        _ledger.Given(key, Noon, Noon, Noon);
        await _ledger.GrantAsync(key, 1, TestContext.Current.CancellationToken);

        await SentAsync(Texted());

        Assert.Equal([key], _ledger.Spent);
        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await SendAsync(Texted(), TestContext.Current.CancellationToken)));
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
            new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value),
            Noon,
            Noon,
            Noon,
            Noon,
            Noon);

        await SentAsync(Notice(subject));

        Assert.Single(_mail.Taken);

        _ledger.Given(
            new RestrictionKey("notification.destination", RestrictionKeyKind.Destination, Mailbox.Value),
            Noon,
            Noon,
            Noon,
            Noon,
            Noon);

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await SendAsync(Notice(subject), TestContext.Current.CancellationToken)));
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

        _ledger.Given(new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value), Noon, Noon, Noon);

        await SentAsync(TextedNotice(subject));

        Assert.Single(_sms.Taken);

        _ledger.Given(
            new RestrictionKey("notification.destination", RestrictionKeyKind.Destination, Phone.Value),
            Noon,
            Noon,
            Noon,
            Noon,
            Noon);

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await SendAsync(TextedNotice(subject), TestContext.Current.CancellationToken)));
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

        Assert.Equal([new RestrictionKey("tenant.sends", RestrictionKeyKind.Host, "acme")], _ledger.Keys);

        Result<SendReference> second = await SendAsync(
            Texted(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(second));
        Assert.Equal(Noon + TimeSpan.FromHours(1), RetryAt(second));
    }

    /// <summary>
    /// LIB-HOST-001 AC5: a host's supplier is asked once for each send a restriction
    /// counting under its key applies to, however many such restrictions there are,
    /// and never for a send none of them applies to; what it answers is the key the
    /// buckets count under.
    /// </summary>
    [Fact]
    public async Task LIB_HOST_001_AC5_TheSupplierIsAskedOncePerSendItsKeyAppliesToAsync()
    {
        int asked = 0;

        _suppliers = RestrictionKeySuppliers.Of(
        [
            new RestrictionKeySupplier(
                "tenant",
                (_, _) =>
                {
                    asked++;

                    return ValueTask.FromResult("acme");
                }),
        ]);

        _configuration.Set(
            Settings.Restrictions,
            [
                new Restriction(
                    "tenant.hourly",
                    RestrictionKeyKind.Host,
                    "tenant",
                    RestrictionPurpose.Verification,
                    [new Bucket(10, TimeSpan.FromHours(1), BucketWindow.Sliding)]),
                new Restriction(
                    "tenant.daily",
                    RestrictionKeyKind.Host,
                    "tenant",
                    RestrictionPurpose.Verification,
                    [new Bucket(20, TimeSpan.FromDays(1), BucketWindow.Sliding)]),
            ]);

        await SentAsync(Texted());

        Assert.Equal(1, asked);

        await SentAsync(Texted());

        Assert.Equal(2, asked);

        await SentAsync(Link());

        Assert.Equal(2, asked);
        Assert.Equal(
            [new RestrictionKey("tenant.daily", RestrictionKeyKind.Host, "acme"), new RestrictionKey("tenant.hourly", RestrictionKeyKind.Host, "acme")],
            _ledger.Keys.OrderBy(key => key.Restriction, StringComparer.Ordinal));
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC3: the refusal a restriction produces is the same for an
    /// address an account holds and one it does not, and carries <c>retryAt</c>.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC3_ARefusedSendAnswersTheSameForEitherAddressAsync()
    {
        _ledger.Given(new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value), Noon, Noon, Noon);

        Result<SendReference> unregistered = await SendAsync(
            Texted(),
            TestContext.Current.CancellationToken);

        Result<SendReference> registered = await SendAsync(
            Texted() with { Subject = SubjectId.New(_randomness) },
            TestContext.Current.CancellationToken);

        Assert.Equal(Refusal(unregistered), Refusal(registered));
        Assert.Equal(RetryAt(unregistered), RetryAt(registered));
        Assert.Equal(Written(unregistered), Written(registered));
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC3, AUTH-ABUSE-004 AC14: an ask that sends nothing counts as the
    /// message it stands for would, once for each message that would have been (one mail
    /// in every declared language, one text message for each of them), and is carried by
    /// no transport and written to no outbox; the next send inside the minute is refused
    /// as it would be after the message, and a draw the restrictions refuse is refused in
    /// the same bytes as the send.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC3_ADrawCountsAsTheMessageWouldAndCarriesNothingAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);
        var destination = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value);
        var number = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value);

        Result drawn = await DrawAsync(Mailed() with { Language = null }, TestContext.Current.CancellationToken);
        Result texted = await DrawAsync(Texted() with { Language = null }, TestContext.Current.CancellationToken);

        Assert.True(drawn.Match(() => true, _ => false));
        Assert.True(texted.Match(() => true, _ => false));
        Assert.Equal([Noon], _ledger.Sends(destination));
        Assert.Equal([Noon, Noon], _ledger.Sends(number));
        Assert.Empty(_mail.Taken);
        Assert.Empty(_sms.Taken);
        Assert.Empty(_outbox.Written);

        _clock.Advance(TimeSpan.FromSeconds(10));

        Result<SendReference> sent = await SendAsync(Mailed(), TestContext.Current.CancellationToken);
        Result refused = await DrawAsync(Mailed(), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(sent));
        Assert.Equal(
            Refusal(sent) + Written(sent),
            refused.Match(
                () => throw new Xunit.Sdk.XunitException("The draw was not refused."),
                error => error.Code + JsonSerializer.Serialize(error.Details)));
        Assert.Empty(_mail.Taken);
    }

    /// <summary>
    /// INT-SMS-004 AC2, AUTH-ABUSE-002 AC3: below the floor a draw for a text message
    /// is refused as the text message would be.
    /// </summary>
    [Fact]
    public async Task INT_SMS_004_AC2_ADrawIsRefusedBelowTheFloorAsTheTextWouldBeAsync()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 50m);
        _sms.Balance = 40m;

        Result drawn = await DrawAsync(Texted(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.SmsBalanceFloor,
            drawn.Match(() => (ErrorCode?)null, error => error.Code));
        Assert.Empty(_ledger.Sends(new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value)));
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
            Refusal(await SendAsync(Texted(), TestContext.Current.CancellationToken)));

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

        _ = await SendAsync(Texted(), TestContext.Current.CancellationToken);

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

        await SentAsync(new OutboundMessage(
            SendDestination.Of(Phone),
            MessageKind.Alert,
            RestrictionPurpose.Notification,
            Source: null,
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
        _ledger.Given(new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value), Noon, Noon, Noon);

        await SentAsync(new OutboundMessage(
            SendDestination.Of(Phone),
            MessageKind.Alert,
            RestrictionPurpose.Notification,
            Source: null,
            "en"));

        Assert.Single(_sms.Taken);
    }

    /// <summary>
    /// OPS-ALERT-003 and AUTH-ABUSE-004: an alert is outside every restriction, so it is
    /// carried with every bucket of every restriction that could count it full, and
    /// counts under none (D-166, 342).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AnAlertIsCarriedWithEveryRestrictionsBucketFullAsync()
    {
        _configuration.Set(
            Settings.Restrictions,
            [
                .. Settings.Restrictions.Default,
                new Restriction(
                    "every.send",
                    RestrictionKeyKind.Global,
                    null,
                    RestrictionPurpose.Any,
                    [new Bucket(1, TimeSpan.FromHours(24), BucketWindow.Sliding)]),
                new Restriction(
                    "every.notice",
                    RestrictionKeyKind.Global,
                    null,
                    RestrictionPurpose.Notification,
                    [new Bucket(1, TimeSpan.FromHours(24), BucketWindow.Sliding)]),
            ]);

        foreach (RestrictionKey full in new RestrictionKey[]
        {
            new("sms.destination", RestrictionKeyKind.Destination, Phone.Value),
            new("notification.destination", RestrictionKeyKind.Destination, Phone.Value),
            new("every.send", RestrictionKeyKind.Global, "every.send"),
            new("every.notice", RestrictionKeyKind.Global, "every.notice"),
        })
        {
            _ledger.Given(full, Noon, Noon, Noon, Noon, Noon);
        }

        IReadOnlyCollection<RestrictionKey> before = [.. _ledger.Keys];

        _ = await SentAsync(new OutboundMessage(
            SendDestination.Of(Phone),
            MessageKind.Alert,
            RestrictionPurpose.Notification,
            Source: null,
            "en"));

        Assert.Single(_sms.Taken);
        Assert.Equal(before, _ledger.Keys);
        Assert.All(_ledger.Keys, key => Assert.Equal(5, _ledger.Sends(key).Count));
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
    /// IDN-ATTR-001 AC3: a message to someone whose language nothing names goes out in
    /// every language the deployment declares, each in the deployment's own template for
    /// it. By text it is a message for each language, each under a reference of its own
    /// and each counted, so a report of one failed delivery releases that one alone, and
    /// the reference answered is the first language's (AUTH-ABUSE-004 AC1).
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_AC3_NoKnownLanguageGoesOutInEveryDeclaredOneAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);
        _templates.Set(MessageKind.VerificationCode, SendKind.Sms, "en", new MessageTemplate(null, "english"));
        _templates.Set(MessageKind.VerificationCode, SendKind.Sms, "ar", new MessageTemplate(null, "arabic"));
        var destination = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value);

        SendReference reference = await SentAsync(Texted() with { Language = null });

        Assert.Equal(["english", "arabic"], _sms.Taken.Select(message => message.Text));
        Assert.Equal(reference.Value, _sms.Taken[0].Reference);
        Assert.NotEqual(_sms.Taken[0].Reference, _sms.Taken[1].Reference);
        Assert.Equal([Noon, Noon], _ledger.Sends(destination));
        Assert.Empty(_outbox.Waiting);

        Assert.True(
            await _ledger.ReleaseAsync(
                SendReferences.Of(_sms.Taken[1].Reference),
                TestContext.Current.CancellationToken));
        Assert.Single(_ledger.Sends(destination));
    }

    /// <summary>
    /// IDN-ATTR-001, AUTH-ABUSE-004: where a transport takes one language of a text
    /// message and refuses the next, each was admitted and counts, and the one refused
    /// stays recorded for the retry with its attempt counted.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_ALanguageTheTransportRefusedLeavesTheMessageRecordedAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);
        _templates.Set(MessageKind.VerificationCode, SendKind.Sms, "en", new MessageTemplate(null, "english"));
        _templates.Set(MessageKind.VerificationCode, SendKind.Sms, "ar", new MessageTemplate(null, "arabic"));
        _sms.Takes = 1;

        _ = await SentAsync(Texted() with { Language = null });

        Assert.Equal("english", Assert.Single(_sms.Taken).Text);

        SendDelivery waiting = Assert.Single(_outbox.Waiting);

        Assert.Equal("ar", waiting.Requested.Language);
        Assert.Equal(1, waiting.Attempts);
        Assert.Equal(2, _ledger.Sends(new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value)).Count);
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
            Refusal(await SendAsync(Texted(), TestContext.Current.CancellationToken)));

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
        var untouched = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, "+201009999999");

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
            AccessContext.Of(SubjectId.New(_randomness)),
            TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The edit was refused: {error.Code}."));

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await SendAsync(Texted(), TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-ABUSE-004, LIB-EXT-001 AC4: a deployment that registers its own handler is
    /// still governed. With one that takes everything, the fourth text message to one
    /// number inside a day is refused with <c>retryAt</c> and the handler saw three, each
    /// an admitted message carrying its reference and none of the restrictions' inputs;
    /// below the gateway floor it is asked to carry nothing but an alert.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AReplacedHandlerIsStillGovernedAsync()
    {
        var replaced = new NotificationHandlerInMemory();

        _replaced = replaced;

        for (int sent = 0; sent < 3; sent++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            _ = await SentAsync(Texted());
        }

        Result<SendReference> fourth = await SendAsync(Texted(), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(fourth));
        Assert.Equal(Noon + TimeSpan.FromMinutes(1) + TimeSpan.FromHours(24), RetryAt(fourth));
        Assert.Equal(3, replaced.Asked);
        Assert.Equal(3, replaced.Taken.Select(taken => taken.Reference.Value).Distinct(StringComparer.Ordinal).Count());
        Assert.All(replaced.Taken, taken => Assert.Equal(Phone.Value, taken.Destination.Canonical));
        Assert.Empty(_sms.Taken);

        _configuration.Set(Settings.AbuseSmsBalanceFloor, 5000m);
        _clock.Advance(TimeSpan.FromHours(25));

        Assert.Equal(
            ErrorCodes.SmsBalanceFloor,
            Refusal(await SendAsync(Texted(), TestContext.Current.CancellationToken)));
        Assert.Equal(3, replaced.Asked);

        _ = await SentAsync(Texted() with { Message = MessageKind.Alert });

        Assert.Equal(4, replaced.Asked);
        Assert.Equal(MessageKind.Alert, replaced.Taken[3].Message);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC8, CONV-DESIGN-002 AC5: a send undertaken inside an operation
    /// that then rolls back reaches no transport and leaves no row and no count.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC8_AMessageUndertakenInARolledBackOperationIsNeverCarriedAsync()
    {
        Begun(await _work.BeginAsync(TestContext.Current.CancellationToken));

        Result<SendReference> undertaken = await Path.Send.UndertakeAsync(Mailed(), TestContext.Current.CancellationToken);

        Assert.True(undertaken.Match(_ => true, _ => false));
        Assert.Single(_outbox.Waiting);

        await _work.RollbackAsync();

        Assert.Empty(_mail.Taken);
        Assert.Empty(_outbox.Waiting);
        Assert.Empty(_ledger.Keys);
        Assert.Equal(1, _work.Discarded);

        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_mail.Taken);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC8, CONV-DESIGN-002 AC5: a send undertaken inside an operation
    /// that commits is carried after the commit, and no transport is called while its
    /// transaction is open, however many levels of it are.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC8_AMessageIsCarriedOnlyAfterTheOutermostCommitAsync()
    {
        var open = new List<bool>();

        _mail.Handed = () => open.Add(_work.Open);

        Begun(await _work.BeginAsync(TestContext.Current.CancellationToken));
        Begun(await _work.BeginAsync(TestContext.Current.CancellationToken));

        _ = (await Path.Send.UndertakeAsync(Mailed(), TestContext.Current.CancellationToken))
            .Match(reference => reference, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Begun(await _work.CommitAsync(TestContext.Current.CancellationToken));

        Assert.Empty(_mail.Taken);
        Assert.Single(_outbox.Waiting);
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));

        Begun(await _work.CommitAsync(TestContext.Current.CancellationToken));

        Assert.Single(_mail.Taken);
        Assert.Equal([false], open);
        Assert.Empty(_outbox.Waiting);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC16: a send counts from its admission, whatever becomes of its
    /// delivery, and one that fails for good releases its count and the credit it spent.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC16_ASendCountsFromItsAdmissionAndIsReleasedWhereItFailsForGoodAsync()
    {
        var key = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value);

        _configuration.Set(Settings.OutboxRetryMaxAttempts, 2);
        _ledger.Given(key, Noon.AddMinutes(-6), Noon.AddMinutes(-5), Noon.AddMinutes(-4), Noon.AddMinutes(-3), Noon.AddMinutes(-2));
        await _ledger.GrantAsync(key, 1, TestContext.Current.CancellationToken);
        _mail.Accepts = false;

        _ = await SentAsync(Mailed());

        Assert.Equal(6, _ledger.Sends(key).Count);
        Assert.Equal(0, _ledger.Credit(key));
        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await SendAsync(Mailed(), TestContext.Current.CancellationToken)));

        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_outbox.Waiting);
        Assert.Equal(5, _ledger.Sends(key).Count);
        Assert.Equal(1, _ledger.Credit(key));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC1, AC16: a send in every declared language is judged once. By
    /// email it is one message, composed from each language's text in the declared
    /// order, and counts once. By SMS, with two declared languages and two of the three
    /// sends of a day spent, it is refused whole; with room for both, each counts.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC1_ASendInEveryDeclaredLanguageIsJudgedOnceAsync()
    {
        var mailed = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value);
        var texted = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value);

        _configuration.Set(Settings.NotificationLanguages, Declared);
        _templates.Set(MessageKind.VerificationCode, SendKind.Email, "en", new MessageTemplate("first", "english"));
        _templates.Set(MessageKind.VerificationCode, SendKind.Email, "ar", new MessageTemplate("second", "arabic"));

        _ = await SentAsync(Mailed() with { Language = null });

        MailMessage composed = Assert.Single(_mail.Taken);

        Assert.True(
            composed.Body.IndexOf("english", StringComparison.Ordinal)
            < composed.Body.IndexOf("arabic", StringComparison.Ordinal));
        Assert.True(
            composed.Subject.IndexOf("first", StringComparison.Ordinal)
            < composed.Subject.IndexOf("second", StringComparison.Ordinal));
        Assert.Contains("english", composed.Body, StringComparison.Ordinal);
        Assert.Contains("first", composed.Subject, StringComparison.Ordinal);
        Assert.Single(_ledger.Sends(mailed));

        _ledger.Given(texted, Noon.AddHours(-2), Noon.AddHours(-1));

        Result<SendReference> refused = await SendAsync(
            Texted() with { Language = null },
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(refused));
        Assert.Equal(Noon.AddHours(-2) + TimeSpan.FromHours(24), RetryAt(refused));
        Assert.Empty(_sms.Taken);
        Assert.Equal(2, _ledger.Sends(texted).Count);

        _ledger.Given(texted, Noon.AddHours(-1));

        _ = await SentAsync(Texted() with { Language = null });

        Assert.Equal(2, _sms.Taken.Count);
        Assert.Equal(2, _sms.Taken.Select(message => message.Reference).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, _ledger.Sends(texted).Count);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC15: a recovery link and an invitation link are counted under the
    /// purpose <c>signin</c>, and the confirmation a replace asks of the displaced
    /// address under <c>verification</c>; none of the three is counted or refused by
    /// <c>notification.destination</c>, however full its bucket.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="purpose">The purpose it is undertaken under.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(MessageKind.RecoveryLink, RestrictionPurpose.SignIn)]
    [InlineData(MessageKind.InvitationLink, RestrictionPurpose.SignIn)]
    [InlineData(MessageKind.IdentifierChangeConfirm, RestrictionPurpose.Verification)]
    public async Task AUTH_ABUSE_004_AC15_ALinkAPersonAskedForAnswersToNoNotificationRestrictionAsync(
        MessageKind message,
        RestrictionPurpose purpose)
    {
        var notices = new RestrictionKey("notification.destination", RestrictionKeyKind.Destination, Mailbox.Value);

        _ledger.Given(notices, [.. Enumerable.Range(1, 5).Select(sent => Noon.AddMinutes(-sent)).Order()]);

        _ = await SentAsync(new OutboundMessage(SendDestination.Of(Mailbox), message, purpose, "198.51.100.7", "en")
        {
            Subject = message is MessageKind.InvitationLink ? null : SubjectId.New(_randomness),
        });

        Assert.Equal(5, _ledger.Sends(notices).Count);
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));
    }

    /// <summary>
    /// AUTH-ABUSE-003 AC7, AUTH-ABUSE-004: the message of an ask of a sign-in link, an
    /// email code or a recovery, and the notice to an address no account holds, is
    /// written and counted in the request and carried by the publisher, so the ask is
    /// answered before any transport is called.
    /// </summary>
    /// <param name="message">The message of the ask.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(MessageKind.SignInLink)]
    [InlineData(MessageKind.SignInCode)]
    [InlineData(MessageKind.RecoveryLink)]
    [InlineData(MessageKind.NoAccount)]
    public async Task AUTH_ABUSE_003_AC7_AnAskIsAnsweredBeforeAnyTransportIsCalledAsync(MessageKind message)
    {
        _ = await SentAsync(new OutboundMessage(
            SendDestination.Of(Mailbox),
            message,
            RestrictionPurpose.SignIn,
            "198.51.100.7",
            "en"));

        Assert.Empty(_mail.Taken);
        Assert.Single(_outbox.Waiting);
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_mail.Taken);
        Assert.Empty(_outbox.Waiting);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001 AC4: an attempt whose claim was taken over while
    /// its handler ran records no outcome, and a pass that meets a row another attempt
    /// holds carries nothing, so each row is carried once and each outcome recorded once.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AnAttemptThatDoesNotHoldTheClaimRecordsAndCarriesNothingAsync()
    {
        _outbox.Claiming = claim =>
        {
            _outbox.Claiming = null;
            _outbox.TakeOver(claim.Delivery, claim.Until + TimeSpan.FromMinutes(2));
        };
        _mail.Accepts = false;

        _ = await SentAsync(Mailed());

        SendDelivery held = Assert.Single(_outbox.Waiting);

        Assert.Equal(0, held.Attempts);

        _mail.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_mail.Taken);
        Assert.Single(_outbox.Claimed);
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

    private static OutboundMessage Texted() =>
        new(
            SendDestination.Of(Phone),
            MessageKind.VerificationCode,
            RestrictionPurpose.Verification,
            "198.51.100.7",
            "en");

    private static OutboundMessage Link(SubjectId? subject = null) =>
        new(
            SendDestination.Of(Phone),
            MessageKind.SignInLink,
            RestrictionPurpose.SignIn,
            "198.51.100.7",
            "en")
        {
            Subject = subject,
        };

    private static OutboundMessage Mailed() =>
        new(
            SendDestination.Of(Mailbox),
            MessageKind.VerificationCode,
            RestrictionPurpose.Verification,
            "198.51.100.7",
            "en");

    private static OutboundMessage Notice(SubjectId subject) =>
        new(
            SendDestination.Of(Mailbox),
            MessageKind.SecurityNotice,
            RestrictionPurpose.Notification,
            "198.51.100.7",
            "en")
        {
            Subject = subject,
        };

    private static OutboundMessage TextedNotice(SubjectId subject) =>
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
        (await Path.Publisher.RetryAsync(Carrier, TestContext.Current.CancellationToken)).Match(
            carried => carried,
            error => throw new Xunit.Sdk.XunitException($"The pass failed: {error.Code}."));

    // One send as an operation makes it: undertaken in a unit of work of its own, which
    // commits where the send was admitted, so that its one attempt follows, and rolls
    // back where it was refused.
    private async Task<Result<SendReference>> SendAsync(
        OutboundMessage message,
        System.Threading.CancellationToken cancellationToken)
    {
        Begun(await _work.BeginAsync(cancellationToken));

        Result<SendReference> undertaken = await Path.Send.UndertakeAsync(message, cancellationToken);

        if (undertaken.Match(_ => true, _ => false))
        {
            Begun(await _work.CommitAsync(cancellationToken));
        }
        else
        {
            await _work.RollbackAsync();
        }

        return undertaken;
    }

    // One ask that sends nothing, drawn in a unit of work of its own.
    private async Task<Result> DrawAsync(OutboundMessage message, System.Threading.CancellationToken cancellationToken)
    {
        Begun(await _work.BeginAsync(cancellationToken));

        Result drawn = await Path.Send.DrawAsync(message, cancellationToken);

        if (drawn.Match(() => true, _ => false))
        {
            Begun(await _work.CommitAsync(cancellationToken));
        }
        else
        {
            await _work.RollbackAsync();
        }

        return drawn;
    }

    private static void Begun(Result result) =>
        result.Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The unit of work refused: {error.Code}."));

    private async Task<SendReference> SentAsync(OutboundMessage request) =>
        (await SendAsync(request, TestContext.Current.CancellationToken)).Match(
            reference => reference,
            error => throw new Xunit.Sdk.XunitException($"The send was refused: {error.Code}."));
}
