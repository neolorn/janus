using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The request queue of chapter 09 sections 7 and 8a: what a subject submits for
/// themselves, what an authorised human enters for a request that arrived out of
/// band, and the decision.
/// </summary>
[Trait("kind", "unit")]
public sealed class PrivacyRequestEndpointTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, with a notice published and a zone
    /// the working-day clock is counted in.
    /// </summary>
    public PrivacyRequestEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Documents.Hold(
            new DocumentVersion("privacy-notice", "1", "ar", "النص", [], Noon));
        _deployment.Configuration.Set(
            Janus.Core.Configuration.Settings.PrivacyCalendarTimeZone,
            "Africa/Cairo");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// PRIV-RIGHT-001 AC1, PRIV-RIGHT-002 AC1: the subject submits and is answered
    /// with the receipt and the date the decision is due by.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_ASubmittedRequestIsAnsweredWithItsReceiptAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer submitted = await browser.SendAsync(
            "POST",
            "/privacy/requests",
            ("type", "restriction"),
            ("detail", "the order total is disputed"));

        JsonElement receipt = submitted.Json();

        Assert.Equal(StatusCodes.Status202Accepted, submitted.Status);
        Assert.NotEqual(Guid.Empty, receipt.GetProperty("requestId").GetGuid());
        Assert.NotEqual(default, receipt.GetProperty("receiptSentAt").GetDateTimeOffset());
        Assert.True(
            receipt.GetProperty("decisionDue").GetDateTimeOffset()
            > receipt.GetProperty("receiptSentAt").GetDateTimeOffset());
    }

    /// <summary>
    /// 09 section 7: erasure is not a request type here, so a body asking for one is
    /// malformed rather than a request the queue takes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC1_ErasureIsNotARequestTypeOnThisEndpointAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer submitted = await browser.SendAsync(
            "POST",
            "/privacy/requests",
            ("type", "erasure"),
            ("detail", "please erase me"));

        Assert.Equal(StatusCodes.Status403Forbidden, submitted.Status);
        Assert.Empty(_deployment.Requests.Queue);
    }

    /// <summary>
    /// AUTHZ-CONCEAL-005: the queue is administration, and a customer holding
    /// nothing but their own session is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC2_TheQueueIsRefusedToACustomerAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer queue = await browser.SendAsync("GET", "/admin/privacy/requests/");

        Assert.Equal(StatusCodes.Status403Forbidden, queue.Status);
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC2, 09 section 8a: the authorised human enters an out-of-band
    /// request, and the queue they read back carries the identity confirmation.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC2_AnAuthorisedHumanEntersAnOutOfBandRequestAsync()
    {
        Browser browser = await AuthorisedAsync();
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        Answer entered = await browser.SendAsync(
            "POST",
            "/admin/privacy/requests/",
            ("subject", subject.Value.ToString()),
            ("type", "erasure"),
            ("detail", "a letter asking to be erased"),
            ("receivedAt", "2026-02-27"),
            ("channel", "letter"),
            ("identityConfirmation", "national identity card seen"));

        JsonElement held = Single(await browser.SendAsync("GET", "/admin/privacy/requests/"));

        Assert.Equal(StatusCodes.Status202Accepted, entered.Status);
        Assert.Equal("erasure", held.GetProperty("type").GetString());
        Assert.Equal("open", held.GetProperty("status").GetString());
        Assert.Equal("letter", held.GetProperty("channel").GetString());
        Assert.Equal(
            "national identity card seen",
            held.GetProperty("identityConfirmation").GetString());
    }

    /// <summary>
    /// D-153: a date later than today in the deployment zone is refused, because the
    /// clock would otherwise start later than the statute allows.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_ADateLaterThanTodayIsRefusedAsync()
    {
        Browser browser = await AuthorisedAsync();
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        Answer entered = await browser.SendAsync(
            "POST",
            "/admin/privacy/requests/",
            ("subject", subject.Value.ToString()),
            ("type", "restriction"),
            ("detail", "a letter disputing an order"),
            ("receivedAt", "2099-01-01"),
            ("channel", "letter"),
            ("identityConfirmation", "national identity card seen"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, entered.Status);
        Assert.Equal(
            "privacy.request.receivedfuture",
            entered.Json().GetProperty("code").GetString());
    }

    /// <summary>
    /// 09 section 8a: the decision is the human's, and refusing one records the
    /// reason they gave.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC3_TheDecisionIsRecordedWithItsReasonAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer submitted = await browser.SendAsync(
            "POST",
            "/privacy/requests",
            ("type", "rectification"),
            ("detail", "the recorded total is wrong"));

        Guid request = submitted.Json().GetProperty("requestId").GetGuid();

        Answer refused = await browser.SendAsync(
            "POST",
            "/admin/privacy/requests/" + request.ToString() + "/refuse",
            ("reason", "the figure is the one the bank supplied"));

        JsonElement held = Single(await browser.SendAsync("GET", "/admin/privacy/requests/"));

        Assert.Equal(StatusCodes.Status204NoContent, refused.Status);
        Assert.Equal("refused", held.GetProperty("status").GetString());
        Assert.Equal(
            "the figure is the one the bank supplied",
            held.GetProperty("decisionReason").GetString());
    }

    private static JsonElement Single(Answer answer)
    {
        Assert.Equal(StatusCodes.Status200OK, answer.Status);

        return Assert.Single(answer.Json().EnumerateArray());
    }

    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Administers(Company);
        _deployment.Gate.Grant(subject, Company, Permissions.PrivacyRequestManage);

        return browser;
    }
}
