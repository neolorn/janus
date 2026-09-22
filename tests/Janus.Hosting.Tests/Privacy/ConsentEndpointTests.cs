using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The privacy dashboard of chapter 09 section 7: what the subject sees of their own
/// consents and objections, and that changing one of them is a single request with
/// nothing in the way (PRIV-CONS-008, PRIV-CONS-011, PRIV-RIGHT-001a).
/// </summary>
[Trait("kind", "unit")]
public sealed class ConsentEndpointTests : IAsyncDisposable
{
    private const string Marketing = "marketing";

    private const string Security = "security";

    private const string Fulfilment = "fulfilment";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, with a notice published for the
    /// records to name the version of.
    /// </summary>
    public ConsentEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Documents.Hold(
            new DocumentVersion("privacy-notice", "1", "ar", "النص", [], Noon));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// PRIV-CONS-011 AC1, AC3: what the subject consented to is theirs to read from
    /// the dashboard, carrying the version they were shown and where they said it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_011_AC1_EveryConsentHeldIsVisibleToItsSubjectAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer granted = await browser.SendAsync("POST", "/privacy/consents/" + Marketing + "/grant");
        JsonElement held = Single(await browser.SendAsync("GET", "/privacy/consents"));

        Assert.Equal(StatusCodes.Status204NoContent, granted.Status);
        Assert.Equal(Marketing, held.GetProperty("purpose").GetString());
        Assert.Equal("1", held.GetProperty("noticeVersion").GetString());
        Assert.Equal("dashboard", held.GetProperty("mechanism").GetString());
        Assert.NotEqual(default, held.GetProperty("grantedAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, held.GetProperty("withdrawnAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, held.GetProperty("supersededAt").ValueKind);
    }

    /// <summary>
    /// PRIV-CONS-008 AC1, AC2, AC3, PRIV-CONS-011 AC2: withdrawing takes the one
    /// request granting took, nothing stands between the request and the answer, and
    /// the record it leaves behind carries when it ended.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_008_AC1_WithdrawingTakesTheOneRequestGrantingTookAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer granted = await browser.SendAsync("POST", "/privacy/consents/" + Marketing + "/grant");
        Answer withdrawn = await browser.SendAsync(
            "POST",
            "/privacy/consents/" + Marketing + "/withdraw");

        JsonElement held = Single(await browser.SendAsync("GET", "/privacy/consents"));

        Assert.Equal(granted.Status, withdrawn.Status);
        Assert.Equal(StatusCodes.Status204NoContent, withdrawn.Status);
        Assert.NotEqual(default, held.GetProperty("withdrawnAt").GetDateTimeOffset());
    }

    /// <summary>
    /// PRIV-CONS-008a AC3: a purpose resting on another basis is not the subject's to
    /// agree to, so the dashboard records no consent for it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_008a_AC3_APurposeOnAnotherBasisTakesNoConsentAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync("POST", "/privacy/consents/" + Fulfilment + "/grant");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.PurposeNoConsent.ToString(), refused.Text("code"));
        Assert.Empty((await browser.SendAsync("GET", "/privacy/consents")).Json().EnumerateArray());
    }

    /// <summary>
    /// PRIV-RIGHT-001a AC1, AC2: one switch a purpose on an objectable basis, and
    /// both directions take effect on the request that asked for them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001a_AC1_AnObjectionIsRecordedAndWithdrawnFromTheDashboardAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer objected = await browser.SendAsync("POST", "/privacy/objections/" + Security);
        JsonElement standing = Single(await browser.SendAsync("GET", "/privacy/objections"));

        Answer resumed = await browser.SendAsync("DELETE", "/privacy/objections/" + Security);
        JsonElement ended = Single(await browser.SendAsync("GET", "/privacy/objections"));

        Assert.Equal(StatusCodes.Status204NoContent, objected.Status);
        Assert.Equal(Security, standing.GetProperty("purpose").GetString());
        Assert.Equal("1", standing.GetProperty("noticeVersion").GetString());
        Assert.Equal(JsonValueKind.Null, standing.GetProperty("withdrawnAt").ValueKind);

        Assert.Equal(StatusCodes.Status204NoContent, resumed.Status);
        Assert.NotEqual(default, ended.GetProperty("withdrawnAt").GetDateTimeOffset());
    }

    /// <summary>
    /// PRIV-RIGHT-001a: a purpose whose declared basis is not objectable is refused
    /// by the code that names why, and not by silence.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001a_AC1_APurposeOnANonObjectableBasisIsRefusedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync("POST", "/privacy/objections/" + Marketing);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.PurposeNotObjectable.ToString(), refused.Text("code"));
    }

    /// <summary>
    /// PRIV-CONS-011 AC1: the dashboard answers the subject it belongs to, so a
    /// browser with no session reads nobody's records.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_011_AC1_ABrowserWithNoSessionReadsNobodysRecordsAsync()
    {
        Answer answered = await new Browser(_deployment).SendAsync("GET", "/privacy/consents");

        Assert.Equal(StatusCodes.Status401Unauthorized, answered.Status);
    }

    private static JsonElement Single(Answer answered)
    {
        Assert.Equal(StatusCodes.Status200OK, answered.Status);

        return answered.Json().EnumerateArray().Single();
    }
}
