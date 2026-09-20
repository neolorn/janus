using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The delivery-report callback, treated as hostile input: what a forged reference
/// buys, what a report of delivery changes, and what a flood costs
/// (AUTH-ABUSE-007, INT-SMS-005, INT-GEN-003, OPS-ALERT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class DeliveryReportsTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly PhoneNumber Phone = Number("+201001234567");

    private const string Gateway = "203.0.113.9";

    private readonly ConfigurationInMemory _configuration = new();
    private readonly SendLedgerInMemory _ledger = new();
    private readonly CallbackLedgerInMemory _callbacks = new();
    private readonly MessageTemplatesInMemory _templates = new();
    private readonly MailTransportInMemory _mail = new();
    private readonly SmsTransportInMemory _sms = new();
    private readonly SmsBalanceLedgerInMemory _balances = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment that has named the one key with no default.
    /// </summary>
    public DeliveryReportsTests() => _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

    private DeliveryReports Reports =>
        new(_configuration, _ledger, _callbacks, _work, _events, _clock);

    private SendingService Sending =>
        new(
            _configuration,
            _ledger,
            _templates,
            _mail,
            _sms,
            RestrictionKeySuppliers.None,
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
    /// AUTH-ABUSE-007 AC1 and INT-GEN-003 AC1: a callback carrying a guessed
    /// reference is rejected and releases nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_007_AC1_AForgedReferenceIsRejectedAsync()
    {
        await SentAsync();

        Assert.Equal(
            ErrorCodes.CallbackRejected,
            Refusal(await Reports.ReportAsync(
                Gateway,
                "AAAAAAAAAAAAAAAAAAAAAA",
                delivered: false,
                TestContext.Current.CancellationToken)));

        Assert.Single(_ledger.Sends(new RestrictionKey("sms.destination", Phone.Value)));
    }

    /// <summary>
    /// AUTH-ABUSE-007 AC2 and INT-SMS-005 AC1: a report that the message arrived
    /// changes nothing at all, so nothing it says can verify a phone.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_007_AC2_AReportOfDeliveryChangesNothingAsync()
    {
        SendReference reference = await SentAsync();

        await ReportedAsync(reference.Value, delivered: true);

        Assert.Single(_ledger.Sends(new RestrictionKey("sms.destination", Phone.Value)));
        Assert.Empty(_events.Of<AlertRaised>());
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC2 and INT-SMS-005 AC3: a report of failed delivery takes the
    /// send back out of every bucket it counted against.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC2_AFailureReportReleasesEveryBucketAsync()
    {
        SendReference reference = await SentAsync();

        Assert.Single(_ledger.Sends(new RestrictionKey("sms.destination", Phone.Value)));
        Assert.Single(_ledger.Sends(new RestrictionKey("sms.source", "198.51.100.7")));

        await ReportedAsync(reference.Value, delivered: false);

        Assert.Empty(_ledger.Sends(new RestrictionKey("sms.destination", Phone.Value)));
        Assert.Empty(_ledger.Sends(new RestrictionKey("sms.source", "198.51.100.7")));
    }

    /// <summary>
    /// INT-SMS-005 AC3: a report naming a send the ledger has settled or never knew
    /// changes nothing and is rejected.
    /// </summary>
    [Fact]
    public async Task INT_SMS_005_AC3_ASettledOrUnknownReferenceChangesNothingAsync()
    {
        SendReference reference = await SentAsync();

        await ReportedAsync(reference.Value, delivered: false);

        Assert.Equal(
            ErrorCodes.CallbackRejected,
            Refusal(await Reports.ReportAsync(
                Gateway,
                reference.Value,
                delivered: false,
                TestContext.Current.CancellationToken)));

        Assert.Empty(_ledger.Sends(new RestrictionKey("sms.destination", Phone.Value)));
    }

    /// <summary>
    /// INT-GEN-003 AC1: a reference of the right shape that nobody drew is rejected,
    /// so knowing what a reference looks like buys nothing.
    /// </summary>
    [Fact]
    public async Task INT_GEN_003_AC1_ACallbackWithAGuessedReferenceIsRejectedAsync()
    {
        await SentAsync();

        Assert.Equal(
            ErrorCodes.CallbackRejected,
            Refusal(await Reports.ReportAsync(
                Gateway,
                SendReference.Draw(_randomness).Value,
                delivered: false,
                TestContext.Current.CancellationToken)));

        Assert.Single(_ledger.Sends(new RestrictionKey("sms.destination", Phone.Value)));
    }

    /// <summary>
    /// INT-GEN-003 AC2: the release of a failed send is the one state a report may
    /// cause. A report that the message arrived advances nothing and announces
    /// nothing, so nothing downstream waits on a callback being true.
    /// </summary>
    [Fact]
    public async Task INT_GEN_003_AC2_ACallbackAdvancesNoStateOfItsOwnAsync()
    {
        SendReference reference = await SentAsync();
        int announced = _events.Published.Count;

        await ReportedAsync(reference.Value, delivered: true);

        Assert.Equal(announced, _events.Published.Count);
        Assert.Single(_ledger.Sends(new RestrictionKey("sms.destination", Phone.Value)));
        Assert.Single(_ledger.Sends(new RestrictionKey("sms.source", "198.51.100.7")));
    }

    /// <summary>
    /// INT-SMS-005 AC1: a forged report verifies no phone. A report of delivery for
    /// a reference nobody drew is taken and does nothing: no count moves, nothing is
    /// announced, and there is no state a phone could be marked verified in.
    /// </summary>
    [Fact]
    public async Task INT_SMS_005_AC1_AForgedReportVerifiesNoPhoneAsync()
    {
        await SentAsync();
        int announced = _events.Published.Count;

        await ReportedAsync(SendReference.Draw(_randomness).Value, delivered: true);

        Assert.Equal(announced, _events.Published.Count);
        Assert.Single(_ledger.Sends(new RestrictionKey("sms.destination", Phone.Value)));
        Assert.Single(_ledger.Sends(new RestrictionKey("sms.source", "198.51.100.7")));
    }

    /// <summary>
    /// INT-GEN-003: a flood from one source is answered before any lookup, so the
    /// endpoint costs the deployment nothing beyond the count it already keeps.
    /// </summary>
    [Fact]
    public async Task ReportAsync_MoreCallbacksAMinuteThanAdmitted_IsRejectedAsync()
    {
        _configuration.Set(Settings.IntegrationCallbackRateLimit, 2);

        SendReference reference = await SentAsync();

        await ReportedAsync(reference.Value, delivered: true);
        await ReportedAsync(reference.Value, delivered: true);

        Assert.Equal(
            ErrorCodes.CallbackRejected,
            Refusal(await Reports.ReportAsync(
                Gateway,
                reference.Value,
                delivered: true,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// INT-GEN-003 AC3: a rejected callback is recorded against the source it came
    /// from, which is what the alert counts.
    /// </summary>
    [Fact]
    public async Task INT_GEN_003_AC3_ARejectedCallbackIsRecordedWithItsSourceAsync()
    {
        Assert.Equal(
            ErrorCodes.CallbackRejected,
            Refusal(await Reports.ReportAsync(
                Gateway,
                reference: null,
                delivered: false,
                TestContext.Current.CancellationToken)));

        Assert.Contains(_callbacks.Counted, callback => callback.Rejected
            && string.Equals(callback.Source, Gateway, StringComparison.Ordinal));
    }

    /// <summary>
    /// OPS-ALERT-001 AC1: more rejections from one source in an hour than the
    /// deployment admits raises the repeated-failure alert.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_001_AC1_RepeatedRejectionsRaiseTheCallbackAlertAsync()
    {
        _configuration.Set(Settings.AlertingCallbackThreshold, 2);

        for (int rejected = 0; rejected < 2; rejected++)
        {
            await RejectedAsync();
        }

        Assert.Empty(_events.Of<AlertRaised>());

        await RejectedAsync();

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.CallbackVerificationFailed, raised.Condition);
        Assert.Equal(AlertSeverity.Normal, raised.Severity);
    }

    /// <summary>
    /// INT-SMS-005 AC2 and AUTH-ABUSE-007: a correlation reference is 128 random
    /// bits in base64url, and two draws never agree.
    /// </summary>
    [Fact]
    public void INT_SMS_005_AC2_ACorrelationReferenceIsUnguessable()
    {
        string[] drawn = [.. Enumerable
            .Range(0, 128)
            .Select(_ => SendReference.Draw(_randomness).Value)];

        Assert.Equal(drawn.Length, drawn.Distinct(StringComparer.Ordinal).Count());
        Assert.All(drawn, reference => Assert.Equal(22, reference.Length));
        Assert.All(drawn, reference => Assert.DoesNotContain('=', reference));
        Assert.All(drawn, reference => Assert.DoesNotContain('+', reference));
        Assert.All(drawn, reference => Assert.DoesNotContain('/', reference));
    }

    /// <summary>
    /// The reference is held as its hash, so a dump of what the ledger keeps hands
    /// nobody a value they could present (AUTH-ABUSE-007).
    /// </summary>
    [Fact]
    public void Fingerprint_AReferenceDrawnForASend_IsNotTheReferenceItself()
    {
        var reference = SendReference.Draw(_randomness);

        Assert.Equal(32, reference.Fingerprint().Length);
        Assert.Equal(reference.Fingerprint(), SendReference.FingerprintOf(reference.Value));
        Assert.NotEqual(reference.Fingerprint(), SendReference.FingerprintOf(reference.Value + "0"));
    }

    private static PhoneNumber Number(string entered) =>
        PhoneNumber.TryParse(entered, out PhoneNumber number)
            ? number
            : throw new Xunit.Sdk.XunitException("The number does not parse.");

    private static ErrorCode Refusal(Result result) =>
        result.Match(
            () => throw new Xunit.Sdk.XunitException("The report was not refused."),
            error => error.Code);

    private async Task RejectedAsync() =>
        Assert.Equal(
            ErrorCodes.CallbackRejected,
            Refusal(await Reports.ReportAsync(
                Gateway,
                reference: null,
                delivered: false,
                TestContext.Current.CancellationToken)));

    private async Task ReportedAsync(string reference, bool delivered) =>
        (await Reports.ReportAsync(
            Gateway,
            reference,
            delivered,
            TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The report was refused: {error.Code}."));

    private async Task<SendReference> SentAsync() =>
        (await Sending.SendAsync(
            new SendRequest(
                SendDestination.Of(Phone),
                MessageKind.VerificationCode,
                RestrictionPurpose.Verification,
                "198.51.100.7",
                "en"),
            TestContext.Current.CancellationToken)).Match(
            reference => reference,
            error => throw new Xunit.Sdk.XunitException($"The send was refused: {error.Code}."));
}
