using System;
using System.Security.Cryptography;
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
    private readonly SendLedgerInMemory _ledger = new();
    private readonly NoticeLedgerInMemory _notices = new();
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
    public NonExistenceNoticeTests() => _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

    private NonExistenceNotice Notice =>
        new(
            _configuration,
            new SendingService(
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
                _randomness),
            _notices,
            _work,
            _events,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-ABUSE-003 AC3: the address is told, and what it is told names nothing
    /// about whoever asked.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_003_AC3_TheAddressIsToldAndTheMessageNamesNobodyAsync()
    {
        _templates.Set(
            MessageKind.NoAccount,
            SendKind.Email,
            "en",
            new MessageTemplate("about your address", "no account here. sign in or recover."));

        Assert.True(await ToldAsync("nobody@example.test"));

        MailMessage sent = Assert.Single(_mail.Taken);

        Assert.Equal("nobody@example.test", sent.Destination.Value);
        Assert.DoesNotContain(Source, sent.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(Source, sent.Subject, StringComparison.Ordinal);
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
        Assert.Single(_mail.Taken);

        _clock.Advance(TimeSpan.FromMinutes(31));

        Assert.True(await ToldAsync("nobody@example.test"));
        Assert.Equal(2, _mail.Taken.Count);
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
    /// The notice carries no value at all, so no template place can be filled with
    /// anything about the request (AUTH-ABUSE-003).
    /// </summary>
    [Fact]
    public async Task TellAsync_ANoticeToAnUnknownAddress_FillsNoTemplatePlaceAsync()
    {
        _templates.Set(
            MessageKind.NoAccount,
            SendKind.Email,
            "en",
            new MessageTemplate("about your address", "asked from {source} for {identifier}"));

        Assert.True(await ToldAsync("nobody@example.test"));

        Assert.Equal("asked from {source} for {identifier}", Assert.Single(_mail.Taken).Body);
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
