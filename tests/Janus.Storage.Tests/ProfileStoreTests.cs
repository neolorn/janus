using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Profiles;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Profiles;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// An account's profile as the <c>profiles</c> row carries it (IDN-ATTR-007,
/// REG-PROF-001, PRIV-RIGHT-005a).
/// </summary>
/// <remarks>
/// The port implementation is tested against the aggregate it translates, with the real
/// database (D-156).
/// </remarks>
[Trait("kind", "integration")]
public sealed class ProfileStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Shown = "Ahmed Ismail";
    private const string Legal = "Ahmed Mohamed Ismail";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Born = new(1994, 3, 7);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REG-PROF-001: the three fields read back as they were written, each in the form
    /// its own rules leave it in.
    /// </summary>
    [Fact]
    public async Task REG_PROF_001_AProfileReadsBackAsItWasWrittenAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await RecordAsync(subject, profile =>
        {
            profile.SetDisplayName(Named(Shown));
            profile.SetLegalName(Full(Legal));
            profile.RecordDateOfBirth(Born);
        });

        await using StoreContext reading = database.Context();
        Profile read = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.Equal(subject, read.Subject);
        Assert.Equal(Shown, read.DisplayName?.Value);
        Assert.Equal(Legal, read.LegalName?.Value);
        Assert.Equal(Born, read.DateOfBirth);
    }

    /// <summary>
    /// An account that has filled nothing in has an empty profile, not a row of empty
    /// strings and not a missing answer.
    /// </summary>
    [Fact]
    public async Task FindBySubjectAsync_AnAccountWithNoRow_ReadsAnEmptyProfileAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext reading = database.Context();
        Profile read = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.True(read.IsEmpty);
        Assert.Equal(subject, read.Subject);
    }

    /// <summary>
    /// A field the account gives up clears its column, so nothing of it is left to
    /// read back.
    /// </summary>
    [Fact]
    public async Task RecordAsync_AFieldGivenUp_ClearsItsColumnAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await RecordAsync(subject, profile =>
        {
            profile.SetDisplayName(Named(Shown));
            profile.SetLegalName(Full(Legal));
            profile.RecordDateOfBirth(Born);
        });

        await RecordAsync(subject, profile =>
        {
            profile.SetLegalName(null);
            profile.ForgetDateOfBirth();
        });

        await using StoreContext reading = database.Context();
        Profile read = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.Equal(Shown, read.DisplayName?.Value);
        Assert.Null(read.LegalName);
        Assert.Null(read.DateOfBirth);

        ProfileRecord stored = await StoredAsync(subject);

        Assert.Null(stored.LegalName);
        Assert.Null(stored.DateOfBirth);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC8: what the columns hold is the scheme's own shape, and no
    /// field of the profile appears in them.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC8_NoProfileFieldIsInTheDatabaseInPlainAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await RecordAsync(subject, profile =>
        {
            profile.SetDisplayName(Named(Shown));
            profile.SetLegalName(Full(Legal));
            profile.RecordDateOfBirth(Born);
        });

        ProfileRecord stored = await StoredAsync(subject);

        Assert.Equal(PersonalDataFormat.Marker, stored.DisplayName![0]);
        Assert.Equal(PersonalDataFormat.Marker, stored.LegalName![0]);
        Assert.Equal(PersonalDataFormat.Marker, stored.DateOfBirth![0]);

        Assert.Equal(-1, stored.DisplayName.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Shown)));
        Assert.Equal(-1, stored.LegalName.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Legal)));
        Assert.Equal(-1, stored.DateOfBirth.AsSpan().IndexOf(Encoding.UTF8.GetBytes("1994-03-07")));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC9: once the subject's key is erased the profile written under
    /// it is unreadable, and the read says so.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC9_TheProfileOfAnErasedSubjectIsNotReadableAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await RecordAsync(subject, profile => profile.SetDisplayName(Named(Shown)));
        await _deployment.EraseAsync(subject);

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await Store(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A field the account did not touch keeps the bytes it was written with, so a save
    /// draws no new initialisation vector for a column nothing changed.
    /// </summary>
    [Fact]
    public async Task RecordAsync_AFieldThatDidNotChange_LeavesItsColumnAsItWasAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await RecordAsync(subject, profile =>
        {
            profile.SetDisplayName(Named(Shown));
            profile.RecordDateOfBirth(Born);
        });

        ProfileRecord before = await StoredAsync(subject);
        byte[] shown = before.DisplayName!;

        await RecordAsync(subject, profile => profile.SetLegalName(Full(Legal)));

        ProfileRecord after = await StoredAsync(subject);

        Assert.Equal(shown, after.DisplayName);
        Assert.NotNull(after.LegalName);
    }

    /// <summary>
    /// A profile recorded for a subject that has no key is a fault in the caller: the
    /// key is written in the transaction that creates the subject.
    /// </summary>
    [Fact]
    public async Task RecordAsync_ASubjectWithNoKey_ThrowsAsync()
    {
        await using StoreContext context = database.Context();

        var profile = Profile.Empty(Subjects.New());
        profile.SetDisplayName(Named(Shown));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store(context).RecordAsync(profile, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static DisplayName Named(string entered)
    {
        Assert.True(DisplayName.TryParse(entered, out DisplayName name));

        return name;
    }

    private static LegalName Full(string entered)
    {
        Assert.True(LegalName.TryParse(entered, out LegalName name));

        return name;
    }

    private ProfileStore Store(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);

    private async Task RecordAsync(SubjectId subject, Action<Profile> change)
    {
        await using StoreContext context = database.Context();
        ProfileStore store = Store(context);

        Profile profile = await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken);
        change(profile);

        await store.RecordAsync(profile, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ProfileRecord> StoredAsync(SubjectId subject)
    {
        await using StoreContext context = database.Context();

        return await context.Profiles
            .SingleAsync(held => held.Subject == subject, TestContext.Current.CancellationToken);
    }
}
