using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The one path every message takes: what the named restrictions decide, what a
/// refusal says, what a send counts against, and what a transport that would not
/// take it leaves behind (AUTH-ABUSE-002, AUTH-ABUSE-004, AUTH-ABUSE-006,
/// INT-SMS-001, INT-SMS-004, INT-GEN-005, OPS-ALERT-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class SendingServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly EmailAddress Mailbox = Address("someone@example.test");

    private static readonly PhoneNumber Phone = Number("+201001234567");

    private readonly ConfigurationInMemory _configuration = new();
    private readonly SendLedgerInMemory _ledger = new();
    private readonly MessageTemplatesInMemory _templates = new();
    private readonly MailTransportInMemory _mail = new();
    private readonly SmsTransportInMemory _sms = new();
    private readonly SmsBalanceLedgerInMemory _balances = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private RestrictionKeySuppliers _suppliers = RestrictionKeySuppliers.None;

    /// <summary>
    /// A deployment that has named the one key with no default.
    /// </summary>
    public SendingServiceTests() => _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

    private SendingService Service =>
        new(
            _configuration,
            _ledger,
            _templates,
            _mail,
            _sms,
            _suppliers,
            new SmsBalance(_configuration, _sms, _balances, _work, _events, _clock),
            _work,
            _events,
            _clock,
            _randomness);

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

    private async Task<SendReference> SentAsync(SendRequest request) =>
        (await Service.SendAsync(request, TestContext.Current.CancellationToken)).Match(
            reference => reference,
            error => throw new Xunit.Sdk.XunitException($"The send was refused: {error.Code}."));
}
