using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The compliance-text endpoints of chapter 09 section 8a: publishing a version of the
/// notice or of any other document, and attaching a translation to one
/// (PRIV-CONS-005, PRIV-CONS-006, PRIV-CONS-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class PublicationEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public PublicationEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Company);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// PRIV-CONS-005: the notice publishes over its own route with the governing text,
    /// its governing language and a translation, and the answer is the version.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_005_TheNoticePublishesOverTheEndpointAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer published = await browser.SendAsync(
            "POST",
            "/admin/notices",
            """
            {
              "text": "The governing text.",
              "governingLanguage": "ar",
              "translations": [ { "language": "en", "text": "The translation." } ],
              "material": false
            }
            """);

        Assert.Equal(StatusCodes.Status200OK, published.Status);
        Assert.Equal("privacy-notice", published.Text("document"));
        Assert.Equal("ar", published.Text("governingLanguage"));
        Assert.Equal("The governing text.", published.Text("text"));
        Assert.Equal("en", Assert.Single(published.Json().GetProperty("translations").EnumerateArray())
            .GetProperty("language").GetString());
        Assert.Contains(_deployment.Documents.Versions, version => version.DocumentName == "privacy-notice");
    }

    /// <summary>
    /// PRIV-CONS-006 AC2: a version of another document without its governing text is
    /// refused with the code chapter 09 names.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_006_AC2_AVersionWithoutGoverningTextIsRefusedOverTheEndpointAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer refused = await browser.SendAsync(
            "POST",
            "/admin/documents/terms/versions",
            """
            { "governingLanguage": "ar", "translations": [ { "language": "en", "text": "Only this." } ], "material": false }
            """);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.NoticeGoverningTextMissing.ToString(), refused.Text("code"));
        Assert.DoesNotContain(_deployment.Documents.Versions, version => version.DocumentName == "terms");
    }

    /// <summary>
    /// PRIV-CONS-007: <c>material</c> is required, so a publication that does not say
    /// is malformed and publishes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_007_APublicationThatDoesNotSayWhetherItIsMaterialIsMalformedAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer refused = await browser.SendAsync(
            "POST",
            "/admin/documents/terms/versions",
            """{ "text": "The governing text.", "governingLanguage": "ar" }""");

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal("material", refused.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Empty(_deployment.Documents.Versions);
    }

    /// <summary>
    /// PRIV-CONS-006 AC3: a translation attaches to a published version over the
    /// endpoint and makes no new version.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_006_AC3_ATranslationAttachesWithoutANewVersionAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer published = await browser.SendAsync(
            "POST",
            "/admin/documents/terms/versions",
            """{ "text": "The governing text.", "governingLanguage": "ar", "material": false }""");

        string version = published.Text("version");

        Answer translated = await browser.SendAsync(
            "PUT",
            $"/admin/documents/terms/versions/{version}/translations/en",
            """{ "text": "The translation." }""");

        Assert.Equal(StatusCodes.Status204NoContent, translated.Status);

        DocumentVersion held = Assert.Single(_deployment.Documents.Versions);

        Assert.Equal(version, held.Version);
        Assert.Equal(["en"], held.Translations.Select(translation => translation.Language));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-005 AC1: without <c>notice:publish</c> each is refused forbidden.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_005_AC1_PublicationIsRefusedWithoutThePermissionAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer notice = await browser.SendAsync(
            "POST",
            "/admin/notices",
            """{ "text": "The governing text.", "governingLanguage": "ar", "material": false }""");

        Answer translation = await browser.SendAsync(
            "PUT",
            "/admin/documents/terms/versions/1/translations/en",
            """{ "text": "The translation." }""");

        Assert.Equal(StatusCodes.Status403Forbidden, notice.Status);
        Assert.Equal(StatusCodes.Status403Forbidden, translation.Status);
        Assert.Empty(_deployment.Documents.Versions);
    }

    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Company, Permissions.NoticePublish);

        return browser;
    }
}
