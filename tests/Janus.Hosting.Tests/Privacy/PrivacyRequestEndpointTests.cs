using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
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
            Settings.PrivacyCalendarTimeZone,
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
            ("detail", "the recorded date of birth is disputed"));

        JsonElement receipt = submitted.Json();

        Assert.Equal(StatusCodes.Status202Accepted, submitted.Status);
        Assert.NotEqual(Guid.Empty, receipt.GetProperty("requestId").GetGuid());
        Assert.NotEqual(default, receipt.GetProperty("receiptSentAt").GetDateTimeOffset());
        Assert.True(
            receipt.GetProperty("decisionDue").GetDateTimeOffset()
            > receipt.GetProperty("receiptSentAt").GetDateTimeOffset());
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC1, 09 section 7: a receipt a sending restriction refuses leaves
    /// the request standing. It is answered 202 with <c>receiptSentAt</c> null and its
    /// deadline, the request is queued and committed, and no receipt is sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_AReceiptARestrictionRefusesIsAnsweredNullAndTheRequestStandsAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Notices.Refuses = true;

        int rolledBack = _deployment.Work.RolledBack;

        Answer submitted = await browser.SendAsync(
            "POST",
            "/privacy/requests",
            ("type", "restriction"),
            ("detail", "the recorded date of birth is disputed"));

        JsonElement receipt = submitted.Json();

        Assert.Equal(StatusCodes.Status202Accepted, submitted.Status);
        Assert.Equal(JsonValueKind.Null, receipt.GetProperty("receiptSentAt").ValueKind);
        Assert.NotEqual(default, receipt.GetProperty("decisionDue").GetDateTimeOffset());
        Assert.Equal(
            receipt.GetProperty("requestId").GetGuid(),
            Assert.Single(_deployment.Requests.Queue).Id.Value);
        Assert.Null(Assert.Single(_deployment.Requests.Queue).ReceiptSentAt);
        Assert.False(_deployment.Work.Open);
        Assert.Equal(rolledBack, _deployment.Work.RolledBack);
        Assert.Empty(_deployment.Notices.Told);
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

        Assert.Equal(StatusCodes.Status400BadRequest, submitted.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), submitted.Text("code"));
        Assert.Equal("type", submitted.Json().GetProperty("details").GetProperty("member").GetString());
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
        SubjectId subject = Borne();

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
        SubjectId subject = Borne();

        Answer entered = await browser.SendAsync(
            "POST",
            "/admin/privacy/requests/",
            ("subject", subject.Value.ToString()),
            ("type", "restriction"),
            ("detail", "a letter disputing a recorded address"),
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

    /// <summary>
    /// CONV-CODE-006 AC2: a request whose body carries no detail is refused naming the
    /// member before the service is reached, and the queue takes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_006_AC2_ARequestMissingItsDetailIsRefusedBeforeTheServiceAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer submitted = await browser.SendAsync("POST", "/privacy/requests", ("type", "restriction"));

        Assert.Equal(StatusCodes.Status400BadRequest, submitted.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), submitted.Text("code"));
        Assert.Equal("detail", submitted.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Empty(_deployment.Requests.Queue);
    }

    /// <summary>
    /// API-CONV-002 AC3, CONV-CODE-006 AC3: a detail longer than 1024 characters after
    /// trimming is refused naming it before the service is reached.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_002_AnOverlongDetailIsRefusedBeforeTheServiceAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer submitted = await browser.SendAsync(
            "POST",
            "/privacy/requests",
            ("type", "restriction"),
            ("detail", " " + new string('d', 1025) + " "));

        Assert.Equal(StatusCodes.Status400BadRequest, submitted.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), submitted.Text("code"));
        Assert.Equal("detail", submitted.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Empty(_deployment.Requests.Queue);
    }

    /// <summary>
    /// API-CONV-002 AC3, CONV-CODE-006 AC3: an entry whose channel or identity
    /// confirmation is blank after trimming is refused naming the member before the
    /// service is reached.
    /// </summary>
    /// <param name="member">The member written blank.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("channel")]
    [InlineData("identityConfirmation")]
    public async Task API_CONV_002_AnEntryWithABlankChannelOrConfirmationIsRefusedAsync(string member)
    {
        Browser browser = await AuthorisedAsync();
        SubjectId subject = Borne();

        Answer entered = await browser.SendAsync(
            "POST",
            "/admin/privacy/requests/",
            ("subject", subject.Value.ToString()),
            ("type", "erasure"),
            ("detail", "a letter asking to be erased"),
            ("receivedAt", "2026-02-27"),
            ("channel", member == "channel" ? "   " : "letter"),
            ("identityConfirmation", member == "identityConfirmation" ? "  " : "national identity card seen"));

        Assert.Equal(StatusCodes.Status400BadRequest, entered.Status);
        Assert.Equal(member, entered.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Empty(_deployment.Requests.Queue);
    }

    /// <summary>
    /// PRIV-RIGHT-001, 09 section 8a (D-183): an entry naming a subject no account bears
    /// is refused 422 naming the member, and the queue takes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AnEntryForASubjectNoAccountBearsIsRefusedAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer entered = await browser.SendAsync(
            "POST",
            "/admin/privacy/requests/",
            ("subject", Guid.NewGuid().ToString()),
            ("type", "erasure"),
            ("receivedAt", "2026-02-27"),
            ("channel", "letter"),
            ("identityConfirmation", "national identity card seen"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, entered.Status);
        Assert.Equal(ErrorCodes.RequestInvalid.ToString(), entered.Text("code"));
        Assert.Equal("subject", entered.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Empty(_deployment.Requests.Queue);
    }

    /// <summary>
    /// API-CONV-002 AC3, 09 section 8a: an entry whose body carries no detail is taken,
    /// records none, and reads back from the queue with none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_002_AnEntryWithNoDetailIsTakenAndRecordsNoneAsync()
    {
        Browser browser = await AuthorisedAsync();
        SubjectId subject = Borne();

        Answer entered = await browser.SendAsync(
            "POST",
            "/admin/privacy/requests/",
            ("subject", subject.Value.ToString()),
            ("type", "erasure"),
            ("receivedAt", "2026-02-27"),
            ("channel", "letter"),
            ("identityConfirmation", "national identity card seen"));

        JsonElement held = Single(await browser.SendAsync("GET", "/admin/privacy/requests/"));

        Assert.Equal(StatusCodes.Status202Accepted, entered.Status);
        Assert.Null(Assert.Single(_deployment.Requests.Queue).Detail);
        Assert.Equal(JsonValueKind.Null, held.GetProperty("detail").ValueKind);
    }

    /// <summary>
    /// API-CONV-002 AC3, CONV-CODE-006 AC3: an entry's detail given blank, or longer
    /// than 1024 characters after trimming, is refused naming it before the service is
    /// reached, so a caller without the permission is answered for the body.
    /// </summary>
    /// <param name="character">What the detail is written of.</param>
    /// <param name="length">How many of it.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData('d', 0)]
    [InlineData(' ', 3)]
    [InlineData('d', 1025)]
    public async Task API_CONV_002_AnEntryWithADetailOutsideTheBoundIsRefusedBeforeTheServiceAsync(
        char character,
        int length)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = Borne();

        Answer entered = await browser.SendAsync(
            "POST",
            "/admin/privacy/requests/",
            ("subject", subject.Value.ToString()),
            ("type", "erasure"),
            ("detail", new string(character, length)),
            ("receivedAt", "2026-02-27"),
            ("channel", "letter"),
            ("identityConfirmation", "national identity card seen"));

        Assert.Equal(StatusCodes.Status400BadRequest, entered.Status);
        Assert.Equal("detail", entered.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Empty(_deployment.Requests.Queue);
    }

    /// <summary>
    /// API-CONV-002 AC3: a refusal whose reason is blank after trimming is refused
    /// naming it, and the request stays open.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_002_ARefusalWithABlankReasonIsRefusedAsync()
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
            ("reason", "   "));

        JsonElement held = Single(await browser.SendAsync("GET", "/admin/privacy/requests/"));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal("reason", refused.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal("open", held.GetProperty("status").GetString());
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC5, 09 section 8a: a fulfilment on a session whose proof is no
    /// longer recent is refused 403 <c>auth.stepup.required</c> and the request stays
    /// open; refusing one on the same session is not gated.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC5_AFulfilmentWithoutStepUpIsRefusedAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer submitted = await browser.SendAsync(
            "POST",
            "/privacy/requests",
            ("type", "restriction"),
            ("detail", "the recorded address is disputed"));
        Guid request = submitted.Json().GetProperty("requestId").GetGuid();

        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer fulfilled = await browser.SendAsync(
            "POST",
            "/admin/privacy/requests/" + request.ToString() + "/fulfil");
        JsonElement open = Single(await browser.SendAsync("GET", "/admin/privacy/requests/"));
        Answer refused = await browser.SendAsync(
            "POST",
            "/admin/privacy/requests/" + request.ToString() + "/refuse",
            ("reason", "the address is the one the customer gave"));

        Assert.Equal(StatusCodes.Status403Forbidden, fulfilled.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), fulfilled.Text("code"));
        Assert.Equal("open", open.GetProperty("status").GetString());
        Assert.Equal(StatusCodes.Status204NoContent, refused.Status);
    }

    private static JsonElement Single(Answer answer)
    {
        Assert.Equal(StatusCodes.Status200OK, answer.Status);

        return Assert.Single(answer.Json().EnumerateArray());
    }

    // The signed-in account, as the accounts table of a deployment bears it.
    private SubjectId Borne()
    {
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.AccountStates.Hold(subject, AccountState.Active);

        return subject;
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the caller committed after the gate step and
    /// before the first write refuses the entering, the fulfilling and the refusing of a
    /// request: none is queued by the entry and the one decided on stays open.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesEachDecisionAsync()
    {
        Browser browser = await AuthorisedAsync();
        SubjectId subject = Borne();

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync(
                "POST",
                "/admin/privacy/requests/",
                ("subject", subject.Value.ToString()),
                ("type", "erasure"),
                ("detail", "a letter asking to be erased"),
                ("receivedAt", "2026-02-27"),
                ("channel", "letter"),
                ("identityConfirmation", "national identity card seen")));

        Assert.Empty(_deployment.Requests.Queue);

        Answer submitted = await browser.SendAsync(
            "POST",
            "/privacy/requests",
            ("type", "rectification"),
            ("detail", "the recorded total is wrong"));
        string request = submitted.Json().GetProperty("requestId").GetGuid().ToString();

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("POST", "/admin/privacy/requests/" + request + "/fulfil"));
        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync(
                "POST",
                "/admin/privacy/requests/" + request + "/refuse",
                ("reason", "the figure is the one the bank supplied")));

        Assert.Equal(
            "open",
            Single(await browser.SendAsync("GET", "/admin/privacy/requests/")).GetProperty("status").GetString());
    }

    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = Borne();

        _deployment.Administers(Company);
        _deployment.Gate.Grant(subject, Company, Permissions.PrivacyRequestManage);

        return browser;
    }
}
