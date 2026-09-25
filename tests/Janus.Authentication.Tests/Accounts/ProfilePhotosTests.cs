using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Tests.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// The image an account shows for itself: who shows one at all, what an upload has to
/// pass, and what is stored when it does (IDN-ATTR-002, IDN-ATTR-003, IDN-ATTR-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class ProfilePhotosTests : IAsyncDisposable
{
    private const string Upload = "an-image";

    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly PreferenceDeclarations Declared = PreferenceDeclarations.Of([]);

    private readonly AccountDirectoryInMemory _directory = new(Declared);
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly AccountAuditInMemory _audit = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly ImageCodecInMemory _codec = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly SubjectId _person;
    private readonly OrganizationId _organization;

    /// <summary>
    /// An account of an organization that shows photos, which is the deployment the
    /// administrative organization is (IDN-ATTR-002).
    /// </summary>
    public ProfilePhotosTests()
    {
        _person = SubjectId.New(_randomness);
        _organization = OrganizationId.New(_clock);
        _directory.Stands(_person, AccountState.Active);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// IDN-ATTR-002 AC1: availability is the organization's, read from its key and
    /// from nothing about the account, so an account in no organization shows no photo
    /// and is refused one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_002_AC1_AnAccountInNoOrganizationShowsNoPhotoAsync()
    {
        Result refused = await Photos.SetAsync(Asking, Bytes(Upload), Cancellation);

        Assert.Equal(ErrorCodes.PhotoNotEnabled, Failure(refused).Code);
        Assert.True((await ShownAsync()).IsEmpty);
        Assert.Empty(_audit.Recorded);
    }

    /// <summary>
    /// IDN-ATTR-002 AC2: an organization is given photos by writing its key, so the
    /// same code that refused the account a moment ago accepts its upload.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_002_AC2_AnOrganizationIsGivenPhotosByItsKeyAloneAsync()
    {
        _memberships.Place(_person, _organization);

        Assert.Equal(
            ErrorCodes.PhotoNotEnabled,
            Failure(await Photos.SetAsync(Asking, Bytes(Upload), Cancellation)).Code);

        Enable(_organization);

        Assert.True(Succeeded(await Photos.SetAsync(Asking, Bytes(Upload), Cancellation)));
        Assert.Equal(ImageCodecInMemory.Reencoded(Upload).ToArray(), (await ShownAsync()).ToArray());
    }

    /// <summary>
    /// IDN-ATTR-002: an account of several organizations shows a photo only where
    /// every one of them shows one, which is how a principal of several resolves a
    /// policy (AUTH-PRIN-002).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_002_AnOrganizationThatShowsNoPhotoWithholdsItFromItsMembersAsync()
    {
        var elsewhere = OrganizationId.New(_clock);

        _memberships.Place(_person, _organization);
        _memberships.Place(_person, elsewhere);
        Enable(_organization);

        Result refused = await Photos.SetAsync(Asking, Bytes(Upload), Cancellation);

        Assert.Equal(ErrorCodes.PhotoNotEnabled, Failure(refused).Code);

        Enable(elsewhere);

        Assert.True(Succeeded(await Photos.SetAsync(Asking, Bytes(Upload), Cancellation)));
    }

    /// <summary>
    /// LIB-HOST-001, IDN-ATTR-004: the library reads no image, so a deployment whose
    /// key was turned on without a codec refuses the upload rather than storing bytes
    /// nothing has read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_001_AnUploadIsRefusedWhereTheDeploymentDeclaredNoCodecAsync()
    {
        _memberships.Place(_person, _organization);
        Enable(_organization);

        var photos = new ProfilePhotos(
            _directory,
            new SettingsRestrictionInMemory(),
            _memberships,
            _configuration,
            _audit,
            _work,
            codec: null,
            _clock);

        Result refused = await photos.SetAsync(Asking, Bytes(Upload), Cancellation);

        Assert.Equal(ErrorCodes.PhotoNotEnabled, Failure(refused).Code);
        Assert.True((await ShownAsync()).IsEmpty);
    }

    /// <summary>
    /// IDN-ATTR-004 AC1: what the upload is is decided by reading it, so bytes the
    /// codec does not recognise are refused however the request described them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_004_AC1_AnUploadTheCodecDoesNotRecogniseIsRefusedAsync()
    {
        _memberships.Place(_person, _organization);
        Enable(_organization);
        _codec.Refuses();

        Result refused = await Photos.SetAsync(Asking, Bytes(Upload), Cancellation);

        Assert.Equal(ErrorCodes.PhotoInvalid, Failure(refused).Code);
        Assert.True((await ShownAsync()).IsEmpty);
    }

    /// <summary>
    /// IDN-ATTR-004 AC2: what is stored is what the codec answered and never what
    /// arrived, so the metadata the upload carried is not in the stored image.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_004_AC2_WhatIsStoredIsWhatTheCodecAnsweredAsync()
    {
        _memberships.Place(_person, _organization);
        Enable(_organization);

        Assert.True(Succeeded(await Photos.SetAsync(Asking, Bytes(Upload), Cancellation)));

        Assert.Equal(Upload, Encoding.ASCII.GetString(_codec.Given.Span));
        Assert.Equal(ImageCodecInMemory.Reencoded(Upload).ToArray(), (await ShownAsync()).ToArray());
        Assert.NotEqual(Bytes(Upload).ToArray(), (await ShownAsync()).ToArray());
    }

    /// <summary>
    /// IDN-ATTR-004: the bound the codec holds the image to is the one the deployment
    /// configured, so a deployment that lowers it lowers what is stored without a code
    /// change.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_004_TheCodecIsHandedTheConfiguredLongestSideAsync()
    {
        _memberships.Place(_person, _organization);
        Enable(_organization);
        _configuration.Set(Settings.PhotoMaxDimension, 256);

        Assert.True(Succeeded(await Photos.SetAsync(Asking, Bytes(Upload), Cancellation)));

        Assert.Equal(256, _codec.Dimension);
    }

    /// <summary>
    /// IDN-ATTR-004: an upload longer than <c>photo.maxbytes</c> is refused by the
    /// library before the codec is asked to read it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_004_AnUploadOverTheConfiguredLengthIsRefusedUnreadAsync()
    {
        _memberships.Place(_person, _organization);
        Enable(_organization);
        _configuration.Set(Settings.PhotoMaxBytes, Upload.Length - 1);

        Result refused = await Photos.SetAsync(Asking, Bytes(Upload), Cancellation);

        Assert.Equal(ErrorCodes.PhotoTooLarge, Failure(refused).Code);
        Assert.Equal(0, _codec.Reads);
        Assert.True((await ShownAsync()).IsEmpty);
    }

    /// <summary>
    /// IDN-ATTR-003: the image an account set is the account's to take down, and what
    /// it took down is gone rather than hidden (IDN-PRIN-003).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_003_AnAccountGivesUpTheImageItShowsAsync()
    {
        _memberships.Place(_person, _organization);
        Enable(_organization);

        Assert.True(Succeeded(await Photos.SetAsync(Asking, Bytes(Upload), Cancellation)));
        Assert.True(Succeeded(await Photos.RemoveAsync(Asking, Cancellation)));

        Assert.True((await ShownAsync()).IsEmpty);
        Assert.True((await Photos.ReadAsync(Asking, Cancellation)).Match(read => read, _ => default).IsEmpty);
    }

    /// <summary>
    /// IDN-ATTR-002: an organization that stops showing photos withholds the image
    /// from every read of it, and the account can still take it down, which is the one
    /// thing it could otherwise never do again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_002_AnImageIsWithheldByAPolicyAndStillGivenUpByItsAccountAsync()
    {
        _memberships.Place(_person, _organization);
        Enable(_organization);

        Assert.True(Succeeded(await Photos.SetAsync(Asking, Bytes(Upload), Cancellation)));

        _configuration.Set(Settings.OrganizationPhoto, _organization.ToString(), false);

        Assert.True((await Photos.ReadAsync(Asking, Cancellation)).Match(read => read, _ => default).IsEmpty);
        Assert.False((await ShownAsync()).IsEmpty);

        Assert.True(Succeeded(await Photos.RemoveAsync(Asking, Cancellation)));
        Assert.True((await ShownAsync()).IsEmpty);
    }

    /// <summary>
    /// IDN-AUD-001, REG-PROF-001: a photo is a profile field, so setting one and
    /// giving one up are recorded as a profile change and neither entry carries the
    /// image.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_AUD_001_SettingAndGivingUpAPhotoAreRecordedAsProfileChangesAsync()
    {
        _memberships.Place(_person, _organization);
        Enable(_organization);

        Assert.True(Succeeded(await Photos.SetAsync(Asking, Bytes(Upload), Cancellation)));
        Assert.True(Succeeded(await Photos.RemoveAsync(Asking, Cancellation)));

        Assert.Equal(2, _audit.Recorded.Count);
        Assert.All(_audit.Recorded, entry => Assert.Equal(AuditActions.ProfileChanged, entry.Action));
        Assert.All(_audit.Recorded, entry => Assert.Equal(_person, entry.Subject));
    }

    private static ReadOnlyMemory<byte> Bytes(string upload) => Encoding.ASCII.GetBytes(upload);

    private static bool Succeeded(Result outcome) => outcome.Match(() => true, _ => false);

    private static Error Failure(Result outcome) =>
        outcome.Match(() => throw new InvalidOperationException("It succeeded."), error => error);

    private static System.Threading.CancellationToken Cancellation =>
        TestContext.Current.CancellationToken;

    private ProfilePhotos Photos => new(
        _directory,
        new SettingsRestrictionInMemory(),
        _memberships,
        _configuration,
        _audit,
        _work,
        _codec.Declared,
        _clock);

    private AccessContext Asking => AccessContext.Of(_person);

    private void Enable(OrganizationId organization) =>
        _configuration.Set(Settings.OrganizationPhoto, organization.ToString(), true);

    // What the directory holds, which a read may withhold and a removal empties.
    private async ValueTask<ReadOnlyMemory<byte>> ShownAsync() =>
        await _directory.PhotoAsync(_person, Cancellation);
}
