using System;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The answer to a request made for an address no account holds: the address itself
/// is told, once per window, in a message that names nobody (AUTH-ABUSE-003,
/// OPS-ALERT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class NonExistenceNoticeTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Source = "198.51.100.7";

    private readonly ConfigurationInMemory _configuration = new();
    private readonly NoticeLedgerInMemory _notices = new();
    private readonly NotificationHandlerInMemory _notifications = new();
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
        new(_configuration, _notifications, _notices, _work, _events, _clock);

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

    private async Task<bool> ToldAsync(string address)
    {
        if (!EmailAddress.TryParse(address, out EmailAddress destination))
        {
            throw new Xunit.Sdk.XunitException("The address does not parse.");
        }

        return (await Notice.TellAsync(
            destination,
            Source,
            "en",
            TestContext.Current.CancellationToken)).Match(
            told => told,
            error => throw new Xunit.Sdk.XunitException($"The notice was refused: {error.Code}."));
    }
}
