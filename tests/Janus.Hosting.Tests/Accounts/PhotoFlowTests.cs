using System;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Tests.Accounts;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Accounts;

/// <summary>
/// What a signed-in browser does with the image its account shows: reads it, replaces
/// it, gives it up, and is told nothing where its organization shows none
/// (IDN-ATTR-002 to IDN-ATTR-004, 09 section 6).
/// </summary>
[Trait("kind", "unit")]
public sealed class PhotoFlowTests : IAsyncDisposable
{
    private const string Path = "/account/photo";

    private const string Upload = "an-image";

    private const string Png = "image/png";

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to send, which is what registering a browser needs.
    /// </summary>
    public PhotoFlowTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// 09 section 6: an account that shows no photo is answered as one whose
    /// organization shows none, and neither answer says which it was.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_002_AnAccountThatShowsNoPhotoAndOneWithNoPolicyAnswerAlikeAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer withoutPolicy = await browser.SendAsync("GET", Path);

        ShowsPhotos();

        Answer withoutPhoto = await browser.SendAsync("GET", Path);

        Assert.Equal(StatusCodes.Status404NotFound, withoutPolicy.Status);
        Assert.Equal(StatusCodes.Status404NotFound, withoutPhoto.Status);
        Assert.Equal(withoutPolicy.Body, withoutPhoto.Body);
    }

    /// <summary>
    /// IDN-ATTR-002, IDN-ATTR-004: an upload is re-encoded on the way in, and what
    /// comes back is what the codec answered, served as the JPEG it is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_004_AnUploadIsStoredReencodedAndServedAsAJpegAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        ShowsPhotos();

        Answer uploaded = await browser.SendAsync("PUT", Path, Upload, contentType: Png);
        Answer read = await browser.SendAsync("GET", Path);

        Assert.Equal(StatusCodes.Status204NoContent, uploaded.Status);
        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("image/jpeg", read.Header("Content-Type"));
        Assert.Equal(Encoding.ASCII.GetString(ImageCodecInMemory.Reencoded(Upload).Span), read.Body);
        Assert.Equal(Upload, Encoding.ASCII.GetString(_deployment.Codec.Given.Span));
    }

    /// <summary>
    /// IDN-ATTR-003 AC3: the image is served through the gate the session is, and the
    /// answer carries nothing a shared cache could hand to anyone else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_003_AC3_TheImageIsServedWithNothingACacheCouldShareAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        ShowsPhotos();
        _ = await browser.SendAsync("PUT", Path, Upload, contentType: Png);

        Answer read = await browser.SendAsync("GET", Path);

        Assert.Equal("no-store", read.Header("Cache-Control"));
        Assert.Null(read.Header("ETag"));

        browser.Forget();

        Answer anonymous = await browser.SendAsync("GET", Path);

        Assert.Equal(StatusCodes.Status401Unauthorized, anonymous.Status);
        Assert.Equal("auth.session.expired", anonymous.Text("code"));
    }

    /// <summary>
    /// 09 section 6: an upload to an account whose organization shows no photo is
    /// refused with the code that says so, and the refusal is a 403.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_002_AnUploadIsRefusedWhereTheOrganizationShowsNoPhotoAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync("PUT", Path, Upload, contentType: Png);

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal("identity.photo.notenabled", refused.Text("code"));
    }

    /// <summary>
    /// IDN-ATTR-004: the bytes the codec refuses are refused with their own code, and
    /// the content type the request claimed plays no part in it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_004_AC1_BytesTheCodecRefusesAreRefusedWhateverTheRequestCalledThemAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        ShowsPhotos();
        _deployment.Codec.Refuses();

        Answer refused = await browser.SendAsync("PUT", Path, Upload, contentType: "image/jpeg");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal("identity.photo.invalid", refused.Text("code"));
    }

    /// <summary>
    /// IDN-ATTR-004: an upload longer than <c>photo.maxbytes</c> is refused with the
    /// code for it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_004_AnUploadOverTheConfiguredLengthIsRefusedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        ShowsPhotos();
        _deployment.Configuration.Set(Settings.PhotoMaxBytes, Upload.Length - 1);

        Answer refused = await browser.SendAsync("PUT", Path, Upload, contentType: Png);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal("identity.photo.toolarge", refused.Text("code"));
    }

    /// <summary>
    /// IDN-ATTR-003, IDN-PRIN-003: the account gives up the image it shows, and what
    /// it gave up is answered as an account with no photo.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_003_AnAccountGivesUpTheImageItShowsAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        ShowsPhotos();
        _ = await browser.SendAsync("PUT", Path, Upload, contentType: Png);

        Answer given = await browser.SendAsync("DELETE", Path);
        Answer read = await browser.SendAsync("GET", Path);

        Assert.Equal(StatusCodes.Status204NoContent, given.Status);
        Assert.Equal(StatusCodes.Status404NotFound, read.Status);
    }

    // IDN-ATTR-002: photos are an organization's to show, so the browser's account is
    // placed in one whose key says it shows them.
    private void ShowsPhotos()
    {
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        var organization = OrganizationId.New(_deployment.Clock);

        _deployment.Memberships.Place(subject, organization);
        _deployment.Configuration.Set(Settings.OrganizationPhoto, organization.ToString(), true);
    }
}
