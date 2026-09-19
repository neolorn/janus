using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Profiles;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Profiles;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// An account's photo as the <c>profile_photos</c> row carries it (IDN-ATTR-003,
/// PRIV-RIGHT-005a).
/// </summary>
/// <remarks>
/// The port implementation is tested against the aggregate it translates, with the real
/// database (D-156). The bytes each test writes stand for a re-encoded image; what a
/// photo must be before it reaches the store is the upload path's business
/// (IDN-ATTR-004).
/// </remarks>
[Trait("kind", "integration")]
public sealed class ProfilePhotoStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// IDN-ATTR-003: the bytes are in the database and read back exactly as they were
    /// written.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_003_APhotoReadsBackByteForByteAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        byte[] image = Image();

        await using (JanusDbContext writing = database.Context())
        {
            await Store(writing).RecordAsync(
                ProfilePhoto.Of(subject, image, Noon),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        ProfilePhoto read = Assert.IsType<ProfilePhoto>(
            await Store(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(image, read.Image.ToArray());
        Assert.Equal(Noon, read.UpdatedAt);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC8: the stored column is the scheme's own shape and not the
    /// image, so a dump without the key-encryption key yields no photograph.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC8_ThePhotoIsNotInTheDatabaseInPlainAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        byte[] image = Image();

        await using (JanusDbContext writing = database.Context())
        {
            await Store(writing).RecordAsync(
                ProfilePhoto.Of(subject, image, Noon),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();
        ProfilePhotoRecord stored = await reading.ProfilePhotos
            .SingleAsync(held => held.Subject == subject, TestContext.Current.CancellationToken);

        Assert.Equal(PersonalDataFormat.Marker, stored.Image[0]);
        Assert.Equal(-1, stored.Image.AsSpan().IndexOf(image));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC9: once the subject's key is erased the image is unreadable,
    /// and the read says so rather than yielding the bytes it finds.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC9_ThePhotoOfAnErasedSubjectIsNotReadableAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using (JanusDbContext writing = database.Context())
        {
            await Store(writing).RecordAsync(
                ProfilePhoto.Of(subject, Image(), Noon),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await _deployment.EraseAsync(subject);

        await using JanusDbContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await Store(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An account that shows no photo has none to read, and one it gives up leaves no
    /// row behind: a photo is what the account looks like now and not a record of
    /// something that happened.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_APhotoGivenUp_LeavesNoRowAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using (JanusDbContext writing = database.Context())
        {
            await Store(writing).RecordAsync(
                ProfilePhoto.Of(subject, Image(), Noon),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (JanusDbContext removing = database.Context())
        {
            await Store(removing).RemoveAsync(subject, TestContext.Current.CancellationToken);
            await removing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using JanusDbContext reading = database.Context();

        Assert.Null(await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken));

        Assert.False(await reading.ProfilePhotos
            .AnyAsync(held => held.Subject == subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A photo recorded for a subject that has no key is a fault in the caller.
    /// </summary>
    [Fact]
    public async Task RecordAsync_ASubjectWithNoKey_ThrowsAsync()
    {
        await using JanusDbContext context = database.Context();

        var photo = ProfilePhoto.Of(Subjects.New(), Image(), Noon);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store(context).RecordAsync(photo, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private byte[] Image()
    {
        byte[] bytes = new byte[512];
        _deployment.Randomness.GetBytes(bytes);

        return bytes;
    }

    private ProfilePhotoStore Store(JanusDbContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);
}
