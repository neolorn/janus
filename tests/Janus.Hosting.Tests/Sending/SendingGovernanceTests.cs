using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Callbacks;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Callbacks;
using Janus.Authentication.Tests.Configuration;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Sending;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// The one path every message takes: what the named restrictions decide, what a
/// refusal says, what a send counts against, and what a handler that would not take
/// it leaves behind (AUTH-ABUSE-002, AUTH-ABUSE-004, INT-SMS-001, INT-SMS-005,
/// INT-SMS-006, INT-GEN-005, OPS-ALERT-003), what is considered about a number before
/// a restricted factor goes to it (AUTH-FACT-002b), the languages a message goes out
/// in (IDN-ATTR-001), and how a message the host did not take is handed over again
/// (D-022, D-225, INF-BG-001).
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

    // A deployment whose text messages answer to one restriction that counts each of
    // them and one marked resend, which counts a send once.
    private static readonly IReadOnlyList<Restriction> Paced =
    [
        new Restriction(
            "sms.destination",
            RestrictionKeyKind.Destination,
            HostKeyName: null,
            RestrictionPurpose.Any,
            [new Bucket(10, TimeSpan.FromHours(24), BucketWindow.Sliding)])
        {
            Channel = RestrictionChannel.Sms,
        },
        new Restriction(
            "sms.resend",
            RestrictionKeyKind.Destination,
            HostKeyName: null,
            RestrictionPurpose.Any,
            [new Bucket(1, TimeSpan.FromMinutes(3), BucketWindow.Sliding)])
        {
            Channel = RestrictionChannel.Sms,
            Resend = true,
        },
    ];

    private static readonly RestrictionKey Each = new("sms.destination", RestrictionKeyKind.Destination, Phone.Value);

    private static readonly RestrictionKey Once = new("sms.resend", RestrictionKeyKind.Destination, Phone.Value);

    private static readonly TimeSpan Recency = TimeSpan.FromMinutes(15);

    private static readonly StepUpChallenge Satisfied =
        new(StepUpOutcome.Satisfied, AssuranceLevel.Aal2, PhishingResistant: false, Recency, [], null);

    private readonly ConfigurationInMemory _configuration = new();
    private readonly SendLedgerInMemory _ledger = new();
    private readonly SendOutboxInMemory _outbox = new();
    private readonly SendAuditInMemory _audit = new();
    private readonly NotificationsInMemory _notifications = new();
    private readonly PhoneSignalAuditInMemory _signals = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();

    private RestrictionKeySuppliers _suppliers = RestrictionKeySuppliers.None;

    private PhoneSignalProvider? _provider;

    // One consideration for the whole of a test, as a request's scope holds one, made at
    // its first use so that it reads the provider the test declared.
    private PhoneSignals? _considering;

    private INotificationHandler? _replaced;

    private ILogger<SendPublisher> _log = NullLogger<SendPublisher>.Instance;

    /// <summary>
    /// A deployment administered by whoever edits it here.
    /// </summary>
    public SendingGovernanceTests()
    {
        _ledger.Work = _work;
        _outbox.Work = _work;

        var administrative = OrganizationId.New(_clock);
        _administrative.Organization = administrative;
        _gate.GrantEveryone(administrative, Permissions.SystemAdminister);
        _gate.GrantEveryone(administrative, Permissions.RestrictionEdit);
        _gate.GrantEveryone(administrative, Permissions.RestrictionGrant);
    }

    private SendingPath Path =>
        new(_configuration, _ledger, _outbox, _notifications, _work, _events, _clock, _randomness)
        {
            Suppliers = _suppliers,
            Signals = _considering ??= new PhoneSignals(_provider, _signals, _work, _clock),
            Replaced = _replaced,
            Alerts = _events,
            Log = _log,
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
                _events,
                _work,
                _clock),
            new AdministrativeScope(_gate, _administrative),
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
            _clock.Advance(TimeSpan.FromMinutes(3));
        }

        Result<SendReference> fourth = await SendAsync(
            Texted(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(fourth));
        Assert.Equal(Noon + TimeSpan.FromHours(24), RetryAt(fourth));
        Assert.Equal(3, _notifications.Sms.Taken.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC1: the instant a refusal names is one at which the sliding bucket
    /// it exceeded has room, so the send refused a moment before it is admitted at it.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC1_ASendIsAdmittedAtTheInstantItsRefusalNamedAsync()
    {
        _configuration.Set(Settings.Restrictions, Paced);

        _ = await SentAsync(Texted());

        _clock.Advance(TimeSpan.FromMinutes(3) - TimeSpan.FromTicks(1));

        Result<SendReference> early = await SendAsync(Texted(), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(early));
        Assert.Equal(Noon + TimeSpan.FromMinutes(3), RetryAt(early));

        _clock.Advance(TimeSpan.FromTicks(1));

        _ = await SentAsync(Texted());

        Assert.Equal(2, _notifications.Sms.Taken.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20: under the restrictions as shipped, a second text message to
    /// one number inside three minutes is refused by <c>sms.resend</c>, with the instant
    /// three minutes after the first send's admission, and is admitted at that instant.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC20_ASecondTextInsideThreeMinutesIsRefusedWithTheLiftAsync()
    {
        await SentAsync(Texted());

        _clock.Advance(TimeSpan.FromMinutes(2));

        Result<SendReference> second = await SendAsync(Texted(), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(second));
        Assert.Equal(Noon + TimeSpan.FromMinutes(3), RetryAt(second));
        Assert.Single(_notifications.Sms.Taken);

        _clock.Advance(TimeSpan.FromMinutes(1));

        _ = await SentAsync(Texted());

        Assert.Equal(2, _notifications.Sms.Taken.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20, INT-SMS-005 AC3: under the restrictions as shipped, a failure
    /// report of the first text admits the second at once.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC20_AFailureReportOfTheFirstTextAdmitsTheSecondAtOnceAsync()
    {
        SendReference first = await SentAsync(Texted());

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await SendAsync(Texted(), TestContext.Current.CancellationToken)));

        await ReportedAsync(first, delivered: false);

        _ = await SentAsync(Texted());

        Assert.Equal(2, _notifications.Sms.Taken.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20, INT-SMS-005 AC3: under the restrictions as shipped, a
    /// delivery report of the first text admits the second at once where the first was a
    /// send of purpose <c>secondfactor</c>, and changes nothing otherwise: the second is
    /// refused until three minutes after the first's admission.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="purpose">The purpose it is undertaken under.</param>
    /// <param name="admitted">Whether the report admits the second text at once.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(MessageKind.SecondStepCode, RestrictionPurpose.SecondFactor, true)]
    [InlineData(MessageKind.SignInLink, RestrictionPurpose.SignIn, false)]
    [InlineData(MessageKind.RecoveryLink, RestrictionPurpose.SignIn, false)]
    [InlineData(MessageKind.VerificationLink, RestrictionPurpose.Verification, false)]
    public async Task AUTH_ABUSE_004_AC20_ADeliveryReportAdmitsTheSecondTextOfASecondFactorSendAloneAsync(
        MessageKind message,
        RestrictionPurpose purpose,
        bool admitted)
    {
        OutboundMessage text = Texted() with { Message = message, Purpose = purpose };
        SendReference first = await SentAsync(text);

        await ReportedAsync(first, delivered: true);

        Result<SendReference> second = await SendAsync(text, TestContext.Current.CancellationToken);

        Assert.Equal(admitted, second.Match(_ => true, _ => false));

        if (!admitted)
        {
            Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(second));
            Assert.Equal(Noon + TimeSpan.FromMinutes(3), RetryAt(second));
        }
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20: under the restrictions as shipped, the first text's code
    /// presented right, or its link pressed, admits the second at once.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC20_TheFirstTextsCodeOrLinkAnsweredAdmitsTheSecondAtOnceAsync()
    {
        byte[] answer = RandomNumberGenerator.GetBytes(32);

        Begun(await _work.BeginAsync(TestContext.Current.CancellationToken));

        SendReference first = (await Path.Send.UndertakeAsync(Texted(), TestContext.Current.CancellationToken))
            .Match(reference => reference, error => throw new Xunit.Sdk.XunitException($"The send was refused: {error.Code}."));

        await Path.Send.CarriesAsync(first, answer, TestContext.Current.CancellationToken);

        Begun(await _work.CommitAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await SendAsync(Texted(), TestContext.Current.CancellationToken)));

        await AnsweredAsync(answer);

        _ = await SentAsync(Texted());

        Assert.Equal(2, _notifications.Sms.Taken.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20, AC1, D-225: under the restrictions as shipped, a text message
    /// owed in two declared languages is one message counted once for each language
    /// against <c>sms.destination</c> and once against <c>sms.resend</c>, which does not
    /// refuse it.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC20_ASendInTwoLanguagesCountsOnceAgainstTheShippedResendRestrictionAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);

        _ = await SentAsync(Texted() with { Language = null });

        Assert.Equal(Declared, Assert.Single(_notifications.Sms.Taken).Languages);
        Assert.Equal([Noon, Noon], _ledger.Sends(Each));
        Assert.Equal([Noon], _ledger.Sends(Once));
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
        Assert.Single(_notifications.Mail.Taken);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC1: where a send exceeds several buckets, of one restriction or
    /// of several, the refusal names the earliest instant at which every one of them
    /// has room for it, which is the latest of the instants each of them lifts at.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC1_TheRetryInstantIsWhenEveryExceededBucketHasRoomAsync()
    {
        var mailed = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value);
        var texted = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value);
        var source = new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.7");

        _ledger.Given(mailed, Noon.AddMinutes(-50), Noon.AddMinutes(-40), Noon.AddMinutes(-30), Noon.AddMinutes(-20), Noon);
        _ledger.Given(texted, Noon.AddHours(-3), Noon.AddHours(-2), Noon.AddHours(-1));
        _ledger.Given(source, [.. Enumerable.Range(1, 10).Select(sent => Noon.AddMinutes(-5 * sent)).Order()]);

        _clock.Advance(TimeSpan.FromSeconds(10));

        Result<SendReference> mail = await SendAsync(Mailed(), TestContext.Current.CancellationToken);
        Result<SendReference> text = await SendAsync(Texted(), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(mail));
        Assert.Equal(Noon.AddMinutes(-50) + TimeSpan.FromHours(1), RetryAt(mail));
        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(text));
        Assert.Equal(Noon.AddHours(-3) + TimeSpan.FromHours(24), RetryAt(text));
    }

    /// <summary>
    /// AUTH-ABUSE-004: a restriction governs only the sends on its channel, so a mail
    /// passes a spent <c>sms.destination</c> and a text message a spent
    /// <c>email.destination</c>, and each is counted under its own channel's restriction
    /// alone (D-166, 342).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC10_ARestrictionGovernsOnlyItsChannelAsync()
    {
        var textedMailbox = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Mailbox.Value);
        var mailedPhone = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Phone.Value);

        _ledger.Given(textedMailbox, Noon, Noon, Noon);
        _ledger.Given(mailedPhone, Noon);

        _clock.Advance(TimeSpan.FromSeconds(10));

        _ = await SentAsync(Mailed());
        _ = await SentAsync(Texted());

        Assert.Single(_notifications.Mail.Taken);
        Assert.Single(_notifications.Sms.Taken);
        Assert.Equal(3, _ledger.Sends(textedMailbox).Count);
        Assert.Single(_ledger.Sends(mailedPhone));
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));
        Assert.Single(_ledger.Sends(new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value)));
        Assert.Single(_ledger.Sends(new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.7")));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC11: a security notice to a number its owner holds answers to the
    /// restrictions whose purpose is <c>notification</c> alone, so a spent
    /// <c>sms.source</c> neither refuses it nor counts it (D-166, 342).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC11_ANoticeToAHolderIsNotCountedBySmsSourceAsync()
    {
        var subject = SubjectId.New(_randomness);
        var source = new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.7");
        DateTimeOffset[] spent = [.. Enumerable.Repeat(Noon, 10)];

        _ledger.Given(source, spent);

        _ = await SentAsync(TextedNotice(subject));

        Assert.Single(_notifications.Sms.Taken);
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
    public async Task AUTH_ABUSE_004_AC12_ASendNoRequestAskedForIsCountedUnderNoSourceAsync()
    {
        _ = await SentAsync(Texted() with { Source = null });

        Assert.Single(_notifications.Sms.Taken);
        Assert.DoesNotContain(_ledger.Keys, key => key.Restriction == "sms.source");
        Assert.Equal([Each, Once], _ledger.Keys.OrderBy(key => key.Restriction, StringComparer.Ordinal));
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: what the deployment can learn about the number is asked for
    /// before a restricted factor is carried to it and before the unit of work that
    /// sends it begins, once, and the answer is written down against the entry it was
    /// asked for in that unit of work.
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

            Assert.False(_work.Open);
            Assert.Empty(_notifications.Sms.Taken);

            return ValueTask.FromResult(PhoneSignal.Clear);
        });

        Assert.True(await Path.Signals.AllowsAsync(
            Factor.PhoneLink,
            Phone.Value,
            subject,
            TestContext.Current.CancellationToken));
        Assert.Empty(_signals.Records);

        _ = await SentAsync(Link(subject));

        Assert.Equal([Phone.Value], asked);
        Assert.Equal([(Factor.PhoneLink, PhoneSignal.Clear, subject)], _signals.Records);
    }

    /// <summary>
    /// AUTH-FACT-002b, CONV-DESIGN-003: the provider is the host's callback, so it is
    /// never asked inside an open transaction. A restricted factor undertaken for a
    /// number nothing was asked about before the unit of work began is a fault, and
    /// nothing is recorded or sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_TheProviderIsNeverAskedInsideAUnitOfWorkAsync()
    {
        _provider = new PhoneSignalProvider((_, _) =>
            throw new Xunit.Sdk.XunitException("The provider was asked inside a unit of work."));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await SendAsync(Link(), TestContext.Current.CancellationToken));

        Assert.Empty(_signals.Records);
        Assert.Empty(_outbox.Waiting);
        Assert.Empty(_notifications.Sms.Taken);
    }

    /// <summary>
    /// CONV-DESIGN-003 (D-209): the signal is asked only with no unit of work open, so
    /// asking it inside one is a fault of the calling code: the provider is not asked,
    /// nothing is recorded, and the unit of work open is left as it stood.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_TheSignalAskedInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        var considered = new PhoneSignalAuditInMemory(_work);
        var signals = new PhoneSignals(
            new PhoneSignalProvider((_, _) =>
                throw new Xunit.Sdk.XunitException("The provider was asked inside a unit of work.")),
            considered,
            _work,
            _clock);

        _ = await _work.BeginAsync(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await signals.AllowsAsync(
            Factor.PhoneCode,
            Phone.Value,
            new SubjectId(Guid.NewGuid()),
            TestContext.Current.CancellationToken));

        Assert.Empty(considered.Records);
        Assert.Equal((1, 0, true), (_work.Opened, _work.RolledBack, _work.Open));
    }

    /// <summary>
    /// CONV-DESIGN-003 (D-209): the unit of work that records a <c>risk</c> answer
    /// begins as the outermost, so one that would join a unit of work already open ends
    /// its level with a rollback, which marks the whole, and throws, nothing recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_ARiskAnswersRecordBeginsTheOutermostUnitOfWorkAsync()
    {
        _provider = new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Risk));

        PhoneSignals signals = Path.Signals;

        // The consideration's own audit reads no transaction here, so the open one is
        // met by the begin alone.
        _ = await _work.BeginAsync(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await signals.AllowsAsync(
            Factor.PhoneLink,
            Phone.Value,
            new SubjectId(Guid.NewGuid()),
            TestContext.Current.CancellationToken));

        Assert.Empty(_signals.Records);
        Assert.Equal((2, 1), (_work.Opened, _work.RolledBack));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _work.CommitAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: a reported change is recorded where it is asked, in a unit of
    /// work of its own, and the factor is withheld.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AReportedChangeIsRecordedWhereItIsAskedAsync()
    {
        var subject = new SubjectId(Guid.NewGuid());

        _provider = new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Risk));

        Assert.False(await Path.Signals.AllowsAsync(
            Factor.PhoneLink,
            Phone.Value,
            subject,
            TestContext.Current.CancellationToken));

        Assert.Equal([(Factor.PhoneLink, PhoneSignal.Risk, subject)], _signals.Records);
        Assert.Equal((1, 1, false), (_work.Opened, _work.OutermostCommitted, _work.Open));
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

        Assert.True(await Path.Signals.AllowsAsync(
            Factor.PhoneLink,
            Phone.Value,
            subject,
            TestContext.Current.CancellationToken));

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
        Assert.Single(_notifications.Sms.Taken);
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
    /// it, carried from there, and removed once the host has taken it, so nothing
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
        Assert.Single(_notifications.Mail.Taken);
    }

    /// <summary>
    /// D-022, AUTH-ABUSE-004 AC16: a handler that would not take the message leaves it
    /// in the outbox with the attempt counted, which is what the publisher retries from.
    /// The send was admitted, so it is answered with its reference and counts from its
    /// admission, whatever became of the attempt.
    /// </summary>
    [Fact]
    public async Task D_022_AHandlerRefusalLeavesTheMessageRecordedAsync()
    {
        _notifications.Mail.Accepts = false;

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
        _notifications.Mail.Accepts = false;

        _ = await SendAsync(Mailed(), TestContext.Current.CancellationToken);

        _notifications.Mail.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_notifications.Mail.Taken);
        Assert.Empty(_outbox.Waiting);
        Assert.Equal(
            [Noon + TimeSpan.FromSeconds(30)],
            _ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC9, AC16: a retried send is judged by the restrictions as they
    /// stand when it is retried, with its own count set aside. One they refuse fails for
    /// good: it is not handed to the handler, its row is removed, its count is released
    /// as AC16 has a send that fails for good released, and nothing is raised.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC9_ARetryTheRestrictionsRefuseFailsForGoodAsync()
    {
        var key = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value);

        _notifications.Mail.Accepts = false;

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
        _notifications.Mail.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));
        _ledger.Given(key, Noon + TimeSpan.FromSeconds(20));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_notifications.Mail.Taken);
        Assert.Empty(_outbox.Waiting);
        Assert.Equal([Noon + TimeSpan.FromSeconds(20)], _ledger.Sends(key));
        Assert.Empty(_events.Of<AlertRaised>());

        _clock.Advance(TimeSpan.FromHours(2));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_notifications.Mail.Taken);
    }

    /// <summary>
    /// INT-SMS-006 AC3, IDN-ATTR-001, D-225: a text message owed in every declared
    /// language is one message carrying them all, with one row, so a retry hands the same
    /// message over again under the same reference, counted as two texts as at its
    /// admission.
    /// </summary>
    [Fact]
    public async Task INT_SMS_006_AC3_ARetryHandsTheSameMessageOverUnderItsReferenceAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);
        _notifications.Sms.Accepts = false;

        SendReference reference = (await SendAsync(Texted() with { Language = null }, TestContext.Current.CancellationToken))
            .Match(value => value, error => throw new Xunit.Sdk.XunitException("The send was refused: " + error.Code + "."));

        SendDelivery waiting = Assert.Single(_outbox.Waiting);

        Assert.Equal(Declared, waiting.Languages);
        Assert.Equal(reference, waiting.Reference);

        _notifications.Sms.Accepts = true;
        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(1, await RetriedAsync());

        SendRequest handed = Assert.Single(_notifications.Sms.Taken);

        Assert.Equal(reference, handed.Reference);
        Assert.Equal(Declared, handed.Languages);
        Assert.Empty(_outbox.Waiting);
        Assert.Equal(2, _ledger.Sends(Each).Count);
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
        _notifications.Mail.Accepts = false;

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
        _notifications.Mail.Accepts = false;

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

        Assert.Single(_notifications.Mail.Taken);

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

        Assert.Single(_notifications.Sms.Taken);

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
    /// no handler and written to no outbox; the next send inside the minute is refused
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
        Assert.Empty(_notifications.Mail.Taken);
        Assert.Empty(_notifications.Sms.Taken);
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
        Assert.Empty(_notifications.Mail.Taken);
    }

    /// <summary>
    /// INT-GEN-005 AC1, INT-SMS-006: the field set of a message handed to the host by
    /// mail is its reference, its kind, its channel, its subject, the address, its
    /// languages and its kind's values, and nothing else.
    /// </summary>
    [Fact]
    public async Task INT_GEN_005_AC1_AMailHandedOverCarriesItsExactFieldSetAsync()
    {
        var subject = SubjectId.New(_randomness);

        SendReference reference = await SentAsync(Mailed() with
        {
            Subject = subject,
            Values = new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = "429184" },
        });

        SendRequest carried = Assert.Single(_notifications.Mail.Taken);

        Assert.Equal(reference, carried.Reference);
        Assert.Equal(MessageKind.VerificationCode, carried.Kind);
        Assert.Equal(SendKind.Email, carried.Channel);
        Assert.Equal(subject, carried.Subject);
        Assert.Equal(Mailbox.Value, carried.Destination);
        Assert.Equal(["en"], carried.Languages);
        Assert.Equal("429184", Assert.Single(carried.Values, value => value.Key == "code").Value);
        Assert.Single(carried.Values);
        Assert.Equal(
            ["Reference", "Kind", "Channel", "Subject", "Destination", "Languages", "Values"],
            typeof(SendRequest).GetProperties().Select(property => property.Name));
    }

    /// <summary>
    /// INT-GEN-005 AC1, INT-SMS-006: a text handed to the host carries the number, its
    /// reference, its kind's values and no subject where the send names none.
    /// </summary>
    [Fact]
    public async Task INT_GEN_005_AC1_ATextHandedOverCarriesItsExactFieldSetAsync()
    {
        SendReference reference = await SentAsync(Texted() with
        {
            Values = new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = "429184" },
        });

        SendRequest carried = Assert.Single(_notifications.Sms.Taken);

        Assert.Equal(reference, carried.Reference);
        Assert.Equal(MessageKind.VerificationCode, carried.Kind);
        Assert.Equal(SendKind.Sms, carried.Channel);
        Assert.Null(carried.Subject);
        Assert.Equal(Phone.Value, carried.Destination);
        Assert.Equal(["en"], carried.Languages);
        Assert.Equal("429184", Assert.Single(carried.Values, value => value.Key == "code").Value);
        Assert.Single(carried.Values);
    }

    /// <summary>
    /// IDN-ATTR-001 AC3, INT-SMS-006: a message to someone whose language nothing names
    /// goes out in every language the deployment declares, in their declared order, as
    /// one message under one reference for the host to compose. By text it counts as a
    /// text for each language, and a report that it failed releases them all
    /// (AUTH-ABUSE-004 AC1, D-225).
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_AC3_NoKnownLanguageGoesOutInEveryDeclaredOneAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);

        SendReference reference = await SentAsync(Texted() with { Language = null });

        SendRequest handed = Assert.Single(_notifications.Sms.Taken);

        Assert.Equal(Declared, handed.Languages);
        Assert.Equal(reference, handed.Reference);
        Assert.Equal([Noon, Noon], _ledger.Sends(Each));
        Assert.Empty(_outbox.Waiting);

        Assert.True(
            await _ledger.ReleaseAsync(SendReferences.Of(reference), TestContext.Current.CancellationToken));
        Assert.Empty(_ledger.Sends(Each));
    }

    /// <summary>
    /// IDN-ATTR-001: a message whose language is known goes out in that language
    /// alone.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_AKnownLanguageIsTheOnlyOneSentAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Declared);

        _ = await SentAsync(Mailed() with { Language = "ar" });

        Assert.Equal(["ar"], Assert.Single(_notifications.Mail.Taken).Languages);
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

        Assert.Empty(_notifications.Sms.Taken);
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
    /// an admitted message carrying its reference and none of the restrictions' inputs.
    /// </summary>
    [Fact]
    public async Task LIB_EXT_001_AC4_AReplacedHandlerIsStillGovernedAsync()
    {
        var replaced = new NotificationHandlerInMemory();

        _replaced = replaced;

        for (int sent = 0; sent < 3; sent++)
        {
            _clock.Advance(TimeSpan.FromMinutes(3));
            _ = await SentAsync(Texted());
        }

        Result<SendReference> fourth = await SendAsync(Texted(), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(fourth));
        Assert.Equal(Noon + TimeSpan.FromMinutes(3) + TimeSpan.FromHours(24), RetryAt(fourth));
        Assert.Equal(3, replaced.Asked);
        Assert.Equal(3, replaced.Taken.Select(taken => taken.Reference.Value).Distinct(StringComparer.Ordinal).Count());
        Assert.All(replaced.Taken, taken => Assert.Equal(Phone.Value, taken.Destination));
        Assert.Empty(_notifications.Sms.Taken);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC8, CONV-DESIGN-002 AC5: a send undertaken inside an operation
    /// that then rolls back reaches no handler and leaves no row and no count.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC8_AMessageUndertakenInARolledBackOperationIsNeverCarriedAsync()
    {
        Begun(await _work.BeginAsync(TestContext.Current.CancellationToken));

        Result<SendReference> undertaken = await Path.Send.UndertakeAsync(Mailed(), TestContext.Current.CancellationToken);

        Assert.True(undertaken.Match(_ => true, _ => false));
        Assert.Single(_outbox.Waiting);

        await _work.RollbackAsync();

        Assert.Empty(_notifications.Mail.Taken);
        Assert.Empty(_outbox.Waiting);
        Assert.Empty(_ledger.Keys);
        Assert.Equal(1, _work.Discarded);

        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_notifications.Mail.Taken);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC8, CONV-DESIGN-002 AC5: a send undertaken inside an operation
    /// that commits is carried after the commit, and no handler is called while its
    /// transaction is open, however many levels of it are.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC8_AMessageIsCarriedOnlyAfterTheOutermostCommitAsync()
    {
        var open = new List<bool>();

        _notifications.Mail.Handed = () => open.Add(_work.Open);

        Begun(await _work.BeginAsync(TestContext.Current.CancellationToken));
        Begun(await _work.BeginAsync(TestContext.Current.CancellationToken));

        _ = (await Path.Send.UndertakeAsync(Mailed(), TestContext.Current.CancellationToken))
            .Match(reference => reference, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Begun(await _work.CommitAsync(TestContext.Current.CancellationToken));

        Assert.Empty(_notifications.Mail.Taken);
        Assert.Single(_outbox.Waiting);
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));

        Begun(await _work.CommitAsync(TestContext.Current.CancellationToken));

        Assert.Single(_notifications.Mail.Taken);
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
        _notifications.Mail.Accepts = false;

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
    /// email it is one message carrying the languages in the declared order, and counts
    /// once. By SMS, with two declared languages and two of the three sends of a day
    /// spent, it is refused whole; with room for both, it is one message counted twice.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC1_ASendInEveryDeclaredLanguageIsJudgedOnceAsync()
    {
        var mailed = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value);
        var texted = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Phone.Value);

        _configuration.Set(Settings.NotificationLanguages, Declared);

        _ = await SentAsync(Mailed() with { Language = null });

        Assert.Equal(Declared, Assert.Single(_notifications.Mail.Taken).Languages);
        Assert.Single(_ledger.Sends(mailed));

        _ledger.Given(texted, Noon.AddHours(-2), Noon.AddHours(-1));

        Result<SendReference> refused = await SendAsync(
            Texted() with { Language = null },
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(refused));
        Assert.Equal(Noon.AddHours(-2) + TimeSpan.FromHours(24), RetryAt(refused));
        Assert.Empty(_notifications.Sms.Taken);
        Assert.Equal(2, _ledger.Sends(texted).Count);

        _ledger.Given(texted, Noon.AddHours(-1));

        _ = await SentAsync(Texted() with { Language = null });

        Assert.Equal(Declared, Assert.Single(_notifications.Sms.Taken).Languages);
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
    /// answered before the host's handler is called.
    /// </summary>
    /// <param name="message">The message of the ask.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(MessageKind.SignInLink)]
    [InlineData(MessageKind.SignInCode)]
    [InlineData(MessageKind.RecoveryLink)]
    [InlineData(MessageKind.NoAccount)]
    public async Task AUTH_ABUSE_003_AC7_AnAskIsAnsweredBeforeTheHandlerIsCalledAsync(MessageKind message)
    {
        _ = await SentAsync(new OutboundMessage(
            SendDestination.Of(Mailbox),
            message,
            RestrictionPurpose.SignIn,
            "198.51.100.7",
            "en"));

        Assert.Empty(_notifications.Mail.Taken);
        Assert.Single(_outbox.Waiting);
        Assert.Single(_ledger.Sends(new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value)));

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_notifications.Mail.Taken);
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
        _notifications.Mail.Accepts = false;

        _ = await SentAsync(Mailed());

        SendDelivery held = Assert.Single(_outbox.Waiting);

        Assert.Equal(0, held.Attempts);

        _notifications.Mail.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_notifications.Mail.Taken);
        Assert.Single(_outbox.Claimed);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9: a row an attempt released and rescheduled is not claimed, and
    /// so not carried, before its next attempt's instant has come, by a pass or by an
    /// attempt that follows a commit, which claims whatever the due instant only a row
    /// that has had no attempt.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ARowReleasedAndRescheduledIsNotClaimedBeforeItIsDueAsync()
    {
        _notifications.Mail.Accepts = false;

        _ = await SentAsync(Mailed());

        SendDelivery rescheduled = Assert.Single(_outbox.Waiting);

        Assert.Equal(1, rescheduled.Attempts);
        Assert.True(rescheduled.NextAttemptAt > Noon);

        _notifications.Mail.Accepts = true;
        _clock.Advance(TimeSpan.FromMinutes(3));
        _outbox.Reschedule(rescheduled.Id, _clock.GetUtcNow() + TimeSpan.FromSeconds(1));

        await Path.Publisher.AttemptAsync(rescheduled.Id, TestContext.Current.CancellationToken);

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_notifications.Mail.Taken);
        Assert.Single(_outbox.Claimed);
        Assert.Equal(1, Assert.Single(_outbox.Waiting).Attempts);

        _clock.Advance(TimeSpan.FromSeconds(1));

        await Path.Publisher.AttemptAsync(rescheduled.Id, TestContext.Current.CancellationToken);

        Assert.Single(_notifications.Mail.Taken);
        Assert.Empty(_outbox.Waiting);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC19, CONV-DESIGN-003 AC9: a new row is written due
    /// <c>outbox.retry.initial</c> after its admission, with no jitter, and its immediate
    /// attempt claims it before that instant. Where that attempt left the row unclaimed,
    /// here by a fault at its claim, no pass claims it until the instant has come.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC19_NoPassClaimsANewRowBeforeTheFirstRetryDelayHasPassedAsync()
    {
        _configuration.Set(Settings.OutboxRetryInitial, TimeSpan.FromSeconds(40));
        _outbox.Claiming = _ =>
        {
            _outbox.Claiming = null;

            throw new InvalidOperationException("The claim was not written.");
        };

        _ = await SentAsync(Mailed());
        _clock.Advance(TimeSpan.FromSeconds(39));
        int early = await RetriedAsync();
        int claims = _outbox.Claimed.Count;
        _clock.Advance(TimeSpan.FromSeconds(1));
        int due = await RetriedAsync();

        Assert.Equal(Noon.AddSeconds(40), Assert.Single(_outbox.Written).NextAttemptAt);
        Assert.Equal((0, 1), (early, claims));
        Assert.Equal(1, due);
        Assert.Single(_notifications.Mail.Taken);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC19: a send whose caller acts on whether it was carried counts as
    /// carried where its immediate attempt took it, and as not carried where that attempt
    /// left it waiting.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC19_AFollowedSendIsCarriedOnlyWhereItsImmediateAttemptTookItAsync()
    {
        GovernedSend send = Path.Send;

        IReadOnlyList<SendDeliveryId> taken = await FollowedAsync(send, Mailed());
        _notifications.Sms.Accepts = false;
        IReadOnlyList<SendDeliveryId> left = await FollowedAsync(send, Texted());

        Assert.True(await send.CarriedAsync(taken, TestContext.Current.CancellationToken));
        Assert.False(await send.CarriedAsync(left, TestContext.Current.CancellationToken));
        Assert.Equal(left, _outbox.Waiting.Select(waiting => waiting.Id));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC19: a send whose immediate attempt is not taken counts as not
    /// carried whatever later becomes of its row. A row gone is not a send taken: one
    /// removed at exhaustion by that attempt, and one a later pass carried, both count as
    /// not carried for the caller that followed them.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC19_ASendNotTakenCountsAsNotCarriedWhateverBecomesOfItsRowAsync()
    {
        GovernedSend send = Path.Send;
        _notifications.Mail.Accepts = false;

        IReadOnlyList<SendDeliveryId> later = await FollowedAsync(send, Mailed());
        _notifications.Mail.Accepts = true;
        _clock.Advance(TimeSpan.FromMinutes(3));
        int passed = await RetriedAsync();
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 1);
        _notifications.Sms.Accepts = false;
        IReadOnlyList<SendDeliveryId> exhausted = await FollowedAsync(send, Texted());

        Assert.Equal(1, passed);
        Assert.Empty(_outbox.Waiting);
        Assert.False(await send.CarriedAsync(later, TestContext.Current.CancellationToken));
        Assert.False(await send.CarriedAsync(exhausted, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC12, CONV-ERR-003 AC2: a fault of the library's own in the
    /// immediate attempt after the commit, here the outbox failing at the claim, is
    /// logged and goes no further. The send answers as it committed, its row and its
    /// count stay as written, and the next pass that finds the row due and unclaimed
    /// carries it.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC12_AFaultInTheImmediateAttemptLeavesTheAnswerAndTheRowAsync()
    {
        var key = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value);

        using var logs = new LogsInMemory();
        using ILoggerFactory logging = LoggerFactory.Create(builder => builder.AddProvider(logs));

        _log = logging.CreateLogger<SendPublisher>();
        _outbox.Claiming = _ =>
        {
            _outbox.Claiming = null;

            throw new InvalidOperationException("The claim was not written.");
        };

        SendReference answered = await SentAsync(Mailed());

        SendDelivery held = Assert.Single(_outbox.Waiting);

        Assert.Equal(answered, held.Reference);
        Assert.Equal(0, held.Attempts);
        Assert.Single(_ledger.Sends(key));
        Assert.Empty(_notifications.Mail.Taken);
        Assert.False(_work.Open);

        string line = Assert.Single(logs.Lines, written => written.Contains(typeof(SendPublisher).FullName!, StringComparison.Ordinal));

        Assert.Contains(typeof(InvalidOperationException).FullName!, line, StringComparison.Ordinal);
        Assert.DoesNotContain("The claim was not written.", line, StringComparison.Ordinal);

        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_notifications.Mail.Taken);
        Assert.Empty(_outbox.Waiting);
    }

    /// <summary>
    /// BFF-ERR-002 AC2, D-022: what the host's handler threw at a message is logged as
    /// every other fault the library catches is, by its type and frames and never its
    /// message, and the message waits with the attempt counted.
    /// </summary>
    [Fact]
    public async Task BFF_ERR_002_AC2_WhatTheHandlerThrewAtAMessageIsLoggedAsync()
    {
        using var logs = new LogsInMemory();
        using ILoggerFactory logging = LoggerFactory.Create(builder => builder.AddProvider(logs));

        _log = logging.CreateLogger<SendPublisher>();
        _replaced = new NotificationHandlerInMemory
        {
            Asking = _ => throw new InvalidOperationException("The host's delivery faulted."),
        };

        _ = await SentAsync(Mailed());

        Assert.Equal(1, Assert.Single(_outbox.Waiting).Attempts);

        string line = Assert.Single(logs.Lines, written => written.Contains(typeof(SendPublisher).FullName!, StringComparison.Ordinal));

        Assert.Contains(typeof(InvalidOperationException).FullName!, line, StringComparison.Ordinal);
        Assert.DoesNotContain("The host's delivery faulted.", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// OPS-CFG-008 AC5: a setting that does not read under its key, met by the attempt
    /// that runs after the send committed, is logged there and goes no further: the send
    /// answers as it committed and its row is left to the publisher. The publisher's own
    /// pass, which is a job, fails on the same read as a fault and carries nothing on the
    /// key's default; the row goes out once the value reads.
    /// </summary>
    [Fact]
    public async Task OPS_CFG_008_AC5_ASettingThatDoesNotReadAfterTheCommitIsLoggedAndItsRowLeftAsync()
    {
        using var logs = new LogsInMemory();
        using ILoggerFactory logging = LoggerFactory.Create(builder => builder.AddProvider(logs));

        _log = logging.CreateLogger<SendPublisher>();
        _configuration.Unreachable = Settings.OutboxRetryFactor.Key;

        SendReference answered = await SentAsync(Mailed());

        SendDelivery held = Assert.Single(_outbox.Waiting);

        Assert.Equal(answered, held.Reference);
        Assert.Equal(0, held.Attempts);
        Assert.Empty(_notifications.Mail.Taken);
        Assert.False(_work.Open);
        _ = Assert.Single(logs.Lines, written => written.Contains(typeof(SendPublisher).FullName!, StringComparison.Ordinal));

        _clock.Advance(TimeSpan.FromSeconds(30));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(RetriedAsync);

        Assert.Empty(_notifications.Mail.Taken);
        Assert.Equal(0, Assert.Single(_outbox.Waiting).Attempts);

        _configuration.Unreachable = null;

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_notifications.Mail.Taken);
        Assert.Empty(_outbox.Waiting);
    }
    /// <summary>
    /// PRIV-RIGHT-005a AC15, AUTH-ABUSE-004 AC16: a message whose row's key erasure has
    /// overwritten is never read and never handed to the handler. The pass that meets it
    /// removes the row, and the count the send held from its admission is released.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC15_AMessageWhoseKeyWasErasedIsRemovedUncarriedAndItsCountReleasedAsync()
    {
        var key = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value);

        _notifications.Mail.Accepts = false;

        _ = await SentAsync(Mailed());

        Assert.Single(_ledger.Sends(key));

        _outbox.Erase(Assert.Single(_outbox.Waiting).Id);
        _notifications.Mail.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_notifications.Mail.Taken);
        Assert.Empty(_outbox.Waiting);
        Assert.Empty(_ledger.Sends(key));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC9, AC16: a send that holds no count is judged before it is
    /// carried, whatever its attempts: one written before a send counted from its
    /// admission counts at the instant the restrictions admit it.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC9_ASendThatHoldsNoCountIsJudgedBeforeItIsCarriedAsync()
    {
        var key = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Mailbox.Value);

        _ = await SentAsync(Mailed() with { Message = MessageKind.SignInCode });

        SendDelivery waiting = Assert.Single(_outbox.Waiting);

        Assert.True(await _ledger.ReleaseAsync(SendReferences.Of(waiting.Reference), TestContext.Current.CancellationToken));
        Assert.Empty(_ledger.Sends(key));

        _clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_notifications.Mail.Taken);
        Assert.Equal([Noon + TimeSpan.FromMinutes(2)], _ledger.Sends(key));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC1 and chapter 10 section 5.16a: a text message owed in two
    /// declared languages counts once for each language against a restriction not marked
    /// <c>resend</c> and once against one that is, which judges it as one: its bucket of
    /// one has room for it, and none for the send that follows.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC1_ASendInTwoLanguagesCountsOnceAgainstAResendRestrictionAsync()
    {
        _configuration.Set(Settings.Restrictions, Paced);
        _configuration.Set(Settings.NotificationLanguages, Declared);

        _ = await SentAsync(Texted() with { Language = null });

        Assert.Equal(Declared, Assert.Single(_notifications.Sms.Taken).Languages);
        Assert.Equal([Noon, Noon], _ledger.Sends(Each));
        Assert.Equal([Noon], _ledger.Sends(Once));

        _clock.Advance(TimeSpan.FromMinutes(1));

        Result<SendReference> second = await SendAsync(
            Texted() with { Language = null },
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(second));
        Assert.Equal(Noon + TimeSpan.FromMinutes(3), RetryAt(second));
        Assert.Equal([Noon], _ledger.Sends(Once));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC2, INT-SMS-005 AC2, D-225: a send in two languages is one text
    /// counted twice against the restriction not marked <c>resend</c> and once against
    /// the one that is, so the next send is refused; a failure report of its reference
    /// releases every count, and the next send is admitted at once.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC2_AFailureReportReleasesEveryCountOfATextInEveryLanguageAsync()
    {
        _configuration.Set(Settings.Restrictions, Paced);
        _configuration.Set(Settings.NotificationLanguages, Declared);

        SendReference reference = await SentAsync(Texted() with { Language = null });

        Assert.Equal([Noon, Noon], _ledger.Sends(Each));
        Assert.Equal([Noon], _ledger.Sends(Once));
        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await SendAsync(Texted(), TestContext.Current.CancellationToken)));

        await ReportedAsync(reference, delivered: false);

        Assert.Empty(_ledger.Sends(Each));
        Assert.Empty(_ledger.Sends(Once));

        _ = await SentAsync(Texted());

        Assert.Equal(2, _notifications.Sms.Taken.Count);
    }

    /// <summary>
    /// INT-SMS-005 AC3, AUTH-ABUSE-004 AC20: a report that a text was delivered releases
    /// the one count of a send of purpose <c>secondfactor</c> from the restriction
    /// marked <c>resend</c> and changes nothing else; for a send of any other purpose it
    /// changes nothing at all, since an ask that sends nothing counts too and an early
    /// release would tell its asker that a message went out (AUTH-ABUSE-003).
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="purpose">The purpose it is undertaken under.</param>
    /// <param name="released">Whether the report releases the send's one count.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(MessageKind.SecondStepCode, RestrictionPurpose.SecondFactor, true)]
    [InlineData(MessageKind.SignInLink, RestrictionPurpose.SignIn, false)]
    [InlineData(MessageKind.RecoveryLink, RestrictionPurpose.SignIn, false)]
    [InlineData(MessageKind.VerificationCode, RestrictionPurpose.Verification, false)]
    public async Task INT_SMS_005_AC3_ADeliveryReportReleasesTheResendCountOfASecondFactorSendAloneAsync(
        MessageKind message,
        RestrictionPurpose purpose,
        bool released)
    {
        _configuration.Set(Settings.Restrictions, Paced);

        SendReference reference = await SentAsync(Texted() with { Message = message, Purpose = purpose });

        await ReportedAsync(reference, delivered: true);

        DateTimeOffset[] standing = released ? [] : [Noon];

        Assert.Equal([Noon], _ledger.Sends(Each));
        Assert.Equal(standing, _ledger.Sends(Once));
        Assert.Equal(
            released,
            (await SendAsync(
                Texted() with { Message = message, Purpose = purpose },
                TestContext.Current.CancellationToken)).Match(_ => true, _ => false));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC16: a send all of whose texts fail for good, their attempts
    /// spent, releases each text's count and the send's one count from the restriction
    /// marked <c>resend</c>.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC16_ASendWhoseTextsAllFailForGoodReleasesItsResendCountAsync()
    {
        _configuration.Set(Settings.Restrictions, Paced);
        _configuration.Set(Settings.NotificationLanguages, Declared);
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 2);
        _notifications.Sms.Accepts = false;

        _ = await SentAsync(Texted() with { Language = null });

        Assert.Equal([Noon, Noon], _ledger.Sends(Each));
        Assert.Equal([Noon], _ledger.Sends(Once));

        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(0, await RetriedAsync());
        Assert.Empty(_outbox.Waiting);
        Assert.Empty(_ledger.Sends(Each));
        Assert.Empty(_ledger.Sends(Once));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC16, AC9: a retried text is judged with its own count and its
    /// send's one count set aside, so the restriction marked <c>resend</c> does not
    /// refuse the send its own retry; admitted, each counts at the retry's instant in
    /// place of its earlier count.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC16_ARetriedTextIsJudgedWithItsSendsOneCountSetAsideAsync()
    {
        _configuration.Set(Settings.Restrictions, Paced);
        _notifications.Sms.Accepts = false;

        _ = await SentAsync(Texted());

        _notifications.Sms.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(1, await RetriedAsync());
        Assert.Single(_notifications.Sms.Taken);
        Assert.Equal([Noon + TimeSpan.FromSeconds(30)], _ledger.Sends(Each));
        Assert.Equal([Noon + TimeSpan.FromSeconds(30)], _ledger.Sends(Once));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC9, AC16: a text in two languages that could not be judged at its
    /// retry holds no count against the restriction not marked <c>resend</c> while it
    /// waits and has not failed for good, so the send's one count against the one marked
    /// <c>resend</c> stands; judged at its next attempt, it counts at that instant, once
    /// for each language and the send once, in place of the earlier counts.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC9_ATextNotJudgedAtItsRetryStaysOfItsSendAsync()
    {
        _configuration.Set(Settings.Restrictions, Paced);
        _configuration.Set(Settings.NotificationLanguages, Declared);
        _notifications.Sms.Accepts = false;

        _ = await SentAsync(Texted() with { Language = null });

        Assert.Equal([Noon, Noon], _ledger.Sends(Each));

        // A restriction whose key no supplier answers for: the retry cannot be judged.
        _configuration.Set<IReadOnlyList<Restriction>>(
            Settings.Restrictions,
            [
                .. Paced,
                new Restriction(
                    "tenant.sends",
                    RestrictionKeyKind.Host,
                    "tenant",
                    RestrictionPurpose.Any,
                    [new Bucket(1, TimeSpan.FromHours(1), BucketWindow.Sliding)]),
            ]);
        _notifications.Sms.Accepts = true;
        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(0, await RetriedAsync());
        Assert.Single(_outbox.Waiting);
        Assert.Empty(_ledger.Sends(Each));
        Assert.Equal([Noon], _ledger.Sends(Once));

        _configuration.Set(Settings.Restrictions, Paced);
        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(1, await RetriedAsync());
        Assert.Equal(Declared, Assert.Single(_notifications.Sms.Taken).Languages);
        Assert.Equal([Noon + TimeSpan.FromSeconds(90), Noon + TimeSpan.FromSeconds(90)], _ledger.Sends(Each));
        Assert.Equal([Noon + TimeSpan.FromSeconds(90)], _ledger.Sends(Once));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20, AC2: a send named by the code or the link it carries is
    /// released from the restriction marked <c>resend</c> once that code is presented
    /// right or that link pressed, and from nothing else: the text's counts against the
    /// restriction not marked stand, an answer no send was named by releases nothing,
    /// and the next send is admitted at once.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC20_AnAnswerReleasesTheResendCountOfItsSendAloneAsync()
    {
        _configuration.Set(Settings.Restrictions, Paced);
        _configuration.Set(Settings.NotificationLanguages, Declared);

        byte[] answer = RandomNumberGenerator.GetBytes(32);

        Begun(await _work.BeginAsync(TestContext.Current.CancellationToken));

        SendReference sent = (await Path.Send.UndertakeAsync(
                Texted() with { Language = null },
                TestContext.Current.CancellationToken))
            .Match(reference => reference, error => throw new Xunit.Sdk.XunitException($"The send was refused: {error.Code}."));

        await Path.Send.CarriesAsync(sent, answer, TestContext.Current.CancellationToken);

        Begun(await _work.CommitAsync(TestContext.Current.CancellationToken));

        await AnsweredAsync(RandomNumberGenerator.GetBytes(32));

        Assert.Equal([Noon], _ledger.Sends(Once));
        Assert.Equal(
            ErrorCodes.RestrictionExceeded,
            Refusal(await SendAsync(Texted(), TestContext.Current.CancellationToken)));

        await AnsweredAsync(answer);

        Assert.Equal([Noon, Noon], _ledger.Sends(Each));
        Assert.Empty(_ledger.Sends(Once));

        _ = await SentAsync(Texted());

        Assert.Equal(2, _notifications.Sms.Taken.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC14, AUTH-ABUSE-003: an ask that sends nothing is counted as its
    /// message would be, each language against a restriction not marked <c>resend</c>
    /// and the ask once against one that is, under references no report names and no
    /// code or link answers; the next ask is refused as it would be after a send.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC14_AnAskThatSendsNothingCountsOnceAgainstAResendRestrictionAsync()
    {
        _configuration.Set(Settings.Restrictions, Paced);
        _configuration.Set(Settings.NotificationLanguages, Declared);

        (await DrawAsync(Texted() with { Language = null }, TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The ask was refused: {error.Code}."));

        Assert.Empty(_outbox.Waiting);
        Assert.Empty(_notifications.Sms.Taken);
        Assert.Equal([Noon, Noon], _ledger.Sends(Each));
        Assert.Equal([Noon], _ledger.Sends(Once));

        _clock.Advance(TimeSpan.FromMinutes(2));

        Result<SendReference> next = await SendAsync(
            Texted() with { Language = null },
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refusal(next));
        Assert.Equal(Noon + TimeSpan.FromMinutes(3), RetryAt(next));
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

    // One outcome reported as a host reports it.
    private async Task ReportedAsync(SendReference reference, bool delivered) =>
        _ = (await new SendOutcomes(_ledger, _work)
            .ReportAsync(reference, delivered, TestContext.Current.CancellationToken)).Match(
            named => named,
            error => throw new Xunit.Sdk.XunitException("The report was refused: " + error.Code + "."));

    // A code presented right or a link pressed, in a unit of work of its own.
    private async Task AnsweredAsync(byte[] answer)
    {
        Begun(await _work.BeginAsync(TestContext.Current.CancellationToken));

        await Path.Send.AnsweredAsync(answer, TestContext.Current.CancellationToken);

        Begun(await _work.CommitAsync(TestContext.Current.CancellationToken));
    }

    private async Task<int> RetriedAsync() =>
        (await Path.Publisher.RetryAsync(Carrier, TestContext.Current.CancellationToken)).Match(
            carried => carried,
            error => throw new Xunit.Sdk.XunitException($"The pass failed: {error.Code}."));

    // One send as a caller that follows it makes it: admitted in a unit of work the
    // caller began as the outermost and committed, so that its one attempt has run.
    private async Task<IReadOnlyList<SendDeliveryId>> FollowedAsync(GovernedSend send, OutboundMessage message)
    {
        Begun(await _work.BeginAsync(TestContext.Current.CancellationToken));

        IReadOnlyList<SendDeliveryId> admitted =
            (await send.AdmitAsync(message, TestContext.Current.CancellationToken)).Match(
                written => written,
                error => throw new Xunit.Sdk.XunitException($"The send was refused: {error.Code}."));

        Begun(await _work.CommitAsync(TestContext.Current.CancellationToken));

        return admitted;
    }

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

    private static void Begun(Result<bool> result) =>
        result.Switch(
            _ => { },
            error => throw new Xunit.Sdk.XunitException($"The unit of work refused: {error.Code}."));

    private async Task<SendReference> SentAsync(OutboundMessage request) =>
        (await SendAsync(request, TestContext.Current.CancellationToken)).Match(
            reference => reference,
            error => throw new Xunit.Sdk.XunitException($"The send was refused: {error.Code}."));
}
