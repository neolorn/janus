using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment able to send, which is what registering a browser needs.
    /// </summary>
    public PhotoFlowTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _deployment.DisposeAsync();
        _randomness.Dispose();
    }

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
        Assert.Equal(ErrorCodes.PhotoNotFound.ToString(), withoutPhoto.Text("code"));
        Assert.Equal(withoutPolicy.Text("code"), withoutPhoto.Text("code"));
        Assert.Equal(
            withoutPolicy.Json().GetProperty("details").GetRawText(),
            withoutPhoto.Json().GetProperty("details").GetRawText());
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

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the account committed after the gate step
    /// and before the first write refuses the setting and the removing of a photo, and
    /// the photo stays as it stood.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesAPhotoChangeAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        ShowsPhotos();

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("PUT", Path, Upload, contentType: Png));

        Assert.Equal(StatusCodes.Status404NotFound, (await browser.SendAsync("GET", Path)).Status);
        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync("PUT", Path, Upload, contentType: Png)).Status);

        await RestrictedSinceTheGateStep.RefusesAsync(_deployment, () => browser.SendAsync("DELETE", Path));

        Assert.Equal(StatusCodes.Status200OK, (await browser.SendAsync("GET", Path)).Status);
    }

    /// <summary>
    /// IDN-ATTR-002 AC6 (D-249): an organization whose policy turns photos on over a system
    /// policy that has them off shows its members' photos, while an account of no
    /// organization follows the system policy and sets none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_002_AC6_AnOrganizationShowsPhotosWhereTheSystemPolicyDoesNotAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        var organization = OrganizationId.New(_deployment.Clock);

        _deployment.Configuration.Set(
            Settings.OrganizationPolicy,
            organization.ToString(),
            PolicyOverride.None with { Photos = true });

        Answer withheld = await browser.SendAsync("PUT", Path, Upload, contentType: Png);

        _deployment.Memberships.Place(subject, organization);

        Answer uploaded = await browser.SendAsync("PUT", Path, Upload, contentType: Png);
        Answer read = await browser.SendAsync("GET", Path);

        Assert.Equal(ErrorCodes.PhotoNotEnabled.ToString(), withheld.Text("code"));
        Assert.Equal(StatusCodes.Status204NoContent, uploaded.Status);
        Assert.Equal(StatusCodes.Status200OK, read.Status);
    }

    /// <summary>
    /// IDN-ATTR-003 AC4, IDN-ATTR-002 AC7, `09` `GET /organizations/{id}/members/{account}/photo`
    /// (D-250): a member reads a colleague's photo as `GET /account/photo` serves the
    /// account's own, through the gate with `Cache-Control: no-store` and no entity tag;
    /// every read not admitted (of an organization the reader does not belong to, of a
    /// colleague of a second organization that keeps photos off, of a colleague showing
    /// none) answers 404 `identity.photo.notfound` with the same body.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_003_AC4_AMemberReadsAColleaguesPhotoOverTheWireAsync()
    {
        Browser member = await Flow.SignedInAsync(_deployment);
        SubjectId reader = _deployment.Directory.Created[^1].Subject;
        var colleague = SubjectId.New(_randomness);
        var bare = SubjectId.New(_randomness);
        var divided = SubjectId.New(_randomness);
        var organization = OrganizationId.New(_deployment.Clock);
        var hiding = OrganizationId.New(_deployment.Clock);
        var foreign = OrganizationId.New(_deployment.Clock);

        foreach (SubjectId standing in new[] { colleague, bare, divided })
        {
            _deployment.Accounts.Stands(standing, AccountState.Active);
            _deployment.Memberships.Place(standing, organization);
        }

        _deployment.Memberships.Place(reader, organization);
        _deployment.Memberships.Place(divided, hiding);
        _deployment.Memberships.Place(colleague, foreign);

        foreach (OrganizationId showing in new[] { organization, foreign })
        {
            _deployment.Configuration.Set(
                Settings.OrganizationPolicy,
                showing.ToString(),
                PolicyOverride.None with { Photos = true });
        }
        _deployment.Accounts.Shows(colleague, Encoding.ASCII.GetBytes("a-photo"));
        _deployment.Accounts.Shows(divided, Encoding.ASCII.GetBytes("a-photo"));

        Answer read = await member.SendAsync("GET", ColleagueOf(organization, colleague));
        Answer[] concealed =
        [
            await member.SendAsync("GET", ColleagueOf(foreign, colleague)),
            await member.SendAsync("GET", ColleagueOf(organization, divided)),
            await member.SendAsync("GET", ColleagueOf(organization, bare)),
        ];

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("image/jpeg", read.Header("Content-Type"));
        Assert.Equal("no-store", read.Header("Cache-Control"));
        Assert.Null(read.Header("ETag"));
        Assert.Equal("a-photo", read.Body);
        Assert.All(concealed, answer =>
        {
            Assert.Equal(StatusCodes.Status404NotFound, answer.Status);
            Assert.Equal(ErrorCodes.PhotoNotFound.ToString(), answer.Text("code"));
            Assert.Empty(answer.Json().GetProperty("details").EnumerateObject());
            Assert.Equal(Uncorrelated(concealed[0]), Uncorrelated(answer));
        });
    }

    // The bytes of a refusal save the correlation identifier every answer carries.
    private static string Uncorrelated(Answer answer) =>
        Regex.Replace(answer.Body, "\"correlationId\":\"[^\"]*\"", string.Empty, RegexOptions.None, TimeSpan.FromSeconds(1));

    private static string ColleagueOf(OrganizationId organization, SubjectId member) =>
        "/organizations/" + organization + "/members/" + member + "/photo";

    // IDN-ATTR-002: photos are the policy's to show, and the browser's account belongs
    // to no organization, so the system policy is the one that shows them.
    private void ShowsPhotos() =>
        _deployment.Configuration.Set(Settings.PolicyDefault, Policies.SystemDefault with { Photos = true });
}
