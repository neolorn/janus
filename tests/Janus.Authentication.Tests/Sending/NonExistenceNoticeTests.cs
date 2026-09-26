using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The answer to a request whose message is not going out: an address no account
/// holds is told, once per window, in a message that names nobody, and every other
/// such ask counts against the sending restrictions as the message would have
/// (AUTH-ABUSE-002, AUTH-ABUSE-003, OPS-ALERT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class NonExistenceNoticeTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Source = "198.51.100.7";

    private readonly ConfigurationInMemory _configuration = new();
    private readonly NoticeLedgerInMemory _notices = new();
    private readonly NotificationHandlerInMemory _notifications = new();
    private readonly SendingRestrictionsInMemory _restrictions = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment that has named the keys with no default: the gateway's balance
    /// floor and the languages it writes in.
    /// </summary>
    public NonExistenceNoticeTests()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
        _configuration.Set(Settings.NotificationLanguages, ["en", "ar"]);
    }

    private NonExistenceNotice Notice =>
        new(_configuration, _notifications, _restrictions, _notices, _work, _events, _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// AUTH-ABUSE-003 AC3: the address is told, and what it is told names nothing
    /// about whoever asked.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_003_AC3_TheAddressIsToldAndTheMessageNamesNobodyAsync()
    {
        Assert.True(await ToldAsync("nobody@example.test"));

        SendRequest sent = Assert.Single(_notifications.Mail);

        Assert.Equal("nobody@example.test", sent.Destination.Canonical);
        Assert.Equal(MessageKind.NoAccount, sent.Message);
        Assert.Null(sent.Subject);
        Assert.Empty(sent.Values);
    }

    /// <summary>
    /// AUTH-ABUSE-003 AC4: a second notice to the same address inside the window is
    /// suppressed, so a distributed attacker cannot post that sentence at scale.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_003_AC4_ASecondNoticeInsideTheWindowIsSuppressedAsync()
    {
        Assert.True(await ToldAsync("nobody@example.test"));

        _clock.Advance(TimeSpan.FromMinutes(30));

        Assert.False(await ToldAsync("nobody@example.test"));
        Assert.Single(_notifications.Mail);

        _clock.Advance(TimeSpan.FromMinutes(31));

        Assert.True(await ToldAsync("nobody@example.test"));
        Assert.Equal(2, _notifications.Mail.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-003 AC5: more of these than the deployment admits in an hour
    /// raises the enumeration-probe alert.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_003_AC5_AnUnusualRateRaisesTheProbeAlertAsync()
    {
        _configuration.Set(Settings.AlertingNonexistentThreshold, 2);

        Assert.True(await ToldAsync("one@example.test"));
        Assert.True(await ToldAsync("two@example.test"));

        Assert.Empty(_events.Of<AlertRaised>());

        Assert.True(await ToldAsync("three@example.test"));

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.NonexistentNoticeRate, raised.Condition);
        Assert.Equal(AlertSeverity.Normal, raised.Severity);
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC3, BFF-ABUSE-002 AC1: an ask the window has already answered
    /// sends nothing and still counts against the sending restrictions as the message
    /// it asked for, from the same source and in the language the notice went in.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC3_AnAskTheWindowAnsweredCountsAsTheMessageWouldAsync()
    {
        Assert.True(await ToldAsync("nobody@example.test"));
        Assert.Empty(_restrictions.Drawn);

        Assert.False(await ToldAsync("nobody@example.test"));

        SendRequest drawn = Assert.Single(_restrictions.Drawn);
        SendRequest told = Assert.Single(_notifications.Mail);

        Assert.Equal("nobody@example.test", drawn.Destination.Canonical);
        Assert.Equal(MessageKind.SignInLink, drawn.Message);
        Assert.Equal(RestrictionPurpose.SignIn, drawn.Purpose);
        Assert.Equal(Source, drawn.Source);
        Assert.Equal(told.Language, drawn.Language);
        Assert.Null(drawn.Subject);
        Assert.Empty(drawn.Values);
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC3, AUTH-ABUSE-003: an account the ask cannot reach is told
    /// nothing, leaves the window as it was, and counts as the message would have.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC3_AnAccountTheAskCannotReachIsToldNothingAndCountedAsync()
    {
        Result answered = await AnswerAsync(Address("person@example.test"), unheld: false);

        Assert.True(answered.Match(() => true, _ => false));
        Assert.Empty(_notifications.Sent);
        Assert.Empty(_notices.Told);
        Assert.Equal("person@example.test", Assert.Single(_restrictions.Drawn).Destination.Canonical);
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC3, AUTH-ABUSE-003: a number no account holds is told nothing,
    /// because the notice is mail's alone, and counts as the text would have.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC3_ANumberNoAccountHoldsIsToldNothingAndCountedAsync()
    {
        if (!PhoneNumber.TryParse("+441632960011", out PhoneNumber number))
        {
            throw new Xunit.Sdk.XunitException("The number does not parse.");
        }

        Result answered = await AnswerAsync(SendDestination.Of(number), unheld: true);

        Assert.True(answered.Match(() => true, _ => false));
        Assert.Empty(_notifications.Sent);
        Assert.Empty(_notices.Told);
        Assert.Equal(SendKind.Sms, Assert.Single(_restrictions.Drawn).Kind);
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC3, BFF-ABUSE-002 AC1: where the restrictions refuse the send an
    /// ask stands for, the refusal is the answer, as it is where the message is sent.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC3_ARefusalOfTheRestrictionsIsTheAnswerAsync()
    {
        Assert.True(await ToldAsync("nobody@example.test"));

        var refusal = Error.From(ErrorCodes.RestrictionExceeded);
        _restrictions.Refusal = refusal;

        Result answered = await AnswerAsync(Address("nobody@example.test"), unheld: true);

        Assert.Same(refusal, answered.Match(() => (Error?)null, error => error));
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC3: the notice is the message the ask asked for, so it answers
    /// to that message's restrictions and is refused where the message would be.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC3_TheNoticeAnswersToTheRestrictionsOfTheAskAsync()
    {
        Assert.True(await ToldAsync("nobody@example.test"));

        Result recovered = await Notice.AnswerAsync(
            Address("somebody@example.test"),
            MessageKind.RecoveryLink,
            RestrictionPurpose.Notification,
            Source,
            "en",
            unheld: true,
            TestContext.Current.CancellationToken);

        Assert.True(recovered.Match(() => true, _ => false));
        Assert.All(_notifications.Mail, told => Assert.Equal(MessageKind.NoAccount, told.Message));
        Assert.Equal(
            [RestrictionPurpose.SignIn, RestrictionPurpose.Notification],
            _notifications.Mail.Select(told => told.Purpose));

        var refusal = Error.From(ErrorCodes.RestrictionExceeded);
        _notifications.Refusal = refusal;

        Result refused = await AnswerAsync(Address("elsewhere@example.test"), unheld: true);

        Assert.Same(refusal, refused.Match(() => (Error?)null, error => error));
    }

    private static SendDestination Address(string address) =>
        EmailAddress.TryParse(address, out EmailAddress destination)
            ? SendDestination.Of(destination)
            : throw new Xunit.Sdk.XunitException("The address does not parse.");

    private ValueTask<Result> AnswerAsync(SendDestination destination, bool unheld) =>
        Notice.AnswerAsync(
            destination,
            MessageKind.SignInLink,
            RestrictionPurpose.SignIn,
            Source,
            "en",
            unheld,
            TestContext.Current.CancellationToken);

    private async Task<bool> ToldAsync(string address)
    {
        int before = _notifications.Mail.Count;

        Result answered = await AnswerAsync(Address(address), unheld: true);

        return answered.Match(
            () => _notifications.Mail.Count > before,
            error => throw new Xunit.Sdk.XunitException($"The notice was refused: {error.Code}."));
    }
}
