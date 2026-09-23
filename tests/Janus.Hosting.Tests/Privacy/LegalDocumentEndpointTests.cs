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
/// The compliance-text endpoints of chapter 09 section 7: what they return, and that
/// they return it to a reader who has not signed in (PRIV-CONS-005, PRIV-CONS-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class LegalDocumentEndpointTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// PRIV-CONS-005 AC4: the notice comes back with the governing language of the
    /// version, without a session behind it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_005_AC4_TheNoticeCarriesItsGoverningLanguageAsync()
    {
        await using var deployment = new Deployment();

        await PublishAsync(deployment, "privacy-notice", "1", "النص", []);

        Answer answered = await new Browser(deployment).SendAsync("GET", "/privacy/notice");

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal("privacy-notice", answered.Text("document"));
        Assert.Equal("1", answered.Text("version"));
        Assert.Equal("ar", answered.Text("governingLanguage"));
        Assert.Equal("النص", answered.Text("text"));
    }

    /// <summary>
    /// PRIV-CONS-005 AC3: the governing text and every translation come back
    /// together, so a screen shows either without changing the interface language.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_005_AC3_TheGoverningTextAndTheTranslationsComeBackTogetherAsync()
    {
        await using var deployment = new Deployment();

        await PublishAsync(
            deployment,
            "privacy-notice",
            "1",
            "النص",
            [new DocumentTranslation("en", "The text")]);

        Answer answered = await new Browser(deployment).SendAsync("GET", "/privacy/notice");
        JsonElement translation = answered.Json().GetProperty("translations").EnumerateArray().Single();

        Assert.Equal("النص", answered.Text("text"));
        Assert.Equal("en", translation.GetProperty("language").GetString());
        Assert.Equal("The text", translation.GetProperty("text").GetString());
    }

    /// <summary>
    /// PRIV-CONS-006 AC2: a version named by a consent record resolves to the text
    /// that version carried, whatever has been published since.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_006_AC2_ANamedVersionResolvesToTheTextItCarriedAsync()
    {
        await using var deployment = new Deployment();

        await PublishAsync(deployment, "privacy-notice", "1", "النص الأول", []);
        await PublishAsync(deployment, "privacy-notice", "2", "النص الثاني", []);

        var browser = new Browser(deployment);
        Answer current = await browser.SendAsync("GET", "/privacy/notice");
        Answer named = await browser.SendAsync("GET", "/privacy/notice?version=1");

        Assert.Equal("النص الثاني", current.Text("text"));
        Assert.Equal("النص الأول", named.Text("text"));
        Assert.Equal("1", named.Text("version"));
    }

    /// <summary>
    /// PRIV-CONS-005 AC4: any other document the host publishes is served the same
    /// way and carries its own governing language.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_005_AC4_AnotherDocumentIsServedTheSameWayAsync()
    {
        await using var deployment = new Deployment();

        await PublishAsync(deployment, "terms-of-service", "1", "The terms", [], "en");

        Answer answered = await new Browser(deployment)
            .SendAsync("GET", "/privacy/documents/terms-of-service");

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal("terms-of-service", answered.Text("document"));
        Assert.Equal("en", answered.Text("governingLanguage"));
    }

    /// <summary>
    /// PRIV-CONS-005: a document the deployment never published is refused rather
    /// than answered with an empty version.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_005_AC1_AnUnpublishedDocumentIsRefusedAsync()
    {
        await using var deployment = new Deployment();

        Answer answered = await new Browser(deployment).SendAsync("GET", "/privacy/notice");

        Assert.Equal(StatusCodes.Status404NotFound, answered.Status);
        Assert.Equal(ErrorCodes.DocumentNotFound.ToString(), answered.Text("code"));
    }

    // The store holds versions in the order they were written, so the last published
    // is the current one however the instants read.
    private static async Task PublishAsync(
        Deployment deployment,
        string document,
        string version,
        string text,
        DocumentTranslation[] translations,
        string governing = "ar") =>
        await deployment.Documents.AddAsync(
            new DocumentVersion(document, version, governing, text, translations, Noon),
            CancellationToken.None);
}
