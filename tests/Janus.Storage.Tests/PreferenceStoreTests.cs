using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Preferences;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Preferences;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// An account's preferences as the <c>account_preferences</c> row carries them
/// (IDN-ATTR-001, REG-PREF-001, PRIV-RIGHT-005a).
/// </summary>
/// <remarks>
/// The port implementation is tested against the aggregate it translates, with the real
/// database (D-156). What a value means is the host's business and appears nowhere
/// here.
/// </remarks>
[Trait("kind", "integration")]
public sealed class PreferenceStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// The language, the time zone and every declared value read back as they were
    /// written.
    /// </summary>
    [Fact]
    public async Task RecordAsync_ASetTheAccountSettled_ReadsBackAsItWasWrittenAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using (StoreContext writing = database.Context())
        {
            var preferences = PreferenceSet.Empty(subject);
            preferences.SetLanguage("ar-EG");
            preferences.SetTimeZone("Africa/Cairo");
            preferences.Set(Declared, "theme", "light", asAdministrator: false, Maximum);
            preferences.Set(Declared, "text-size", "18", asAdministrator: false, Maximum);

            await Store(writing).RecordAsync(preferences, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        PreferenceSet read = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.Equal("ar-EG", read.Language);
        Assert.Equal("Africa/Cairo", read.TimeZone);
        Assert.Equal("light", read.Values["theme"]);
        Assert.Equal("18", read.Values["text-size"]);
    }

    /// <summary>
    /// An account that has settled nothing has no row, and reads as a set that has
    /// settled nothing rather than as an absence the caller has to handle.
    /// </summary>
    [Fact]
    public async Task FindBySubjectAsync_AnAccountWithNoRow_ReadsAsAnEmptySetAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext reading = database.Context();
        PreferenceSet read = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.Equal(subject, read.Subject);
        Assert.Null(read.Language);
        Assert.Null(read.TimeZone);
        Assert.Empty(read.Values);
    }

    /// <summary>
    /// A value given up is gone from the row, so an export and a later read carry what
    /// the account holds now.
    /// </summary>
    [Fact]
    public async Task RecordAsync_AValueGivenUp_IsGoneFromTheRowAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using (StoreContext writing = database.Context())
        {
            var preferences = PreferenceSet.Empty(subject);
            preferences.Set(Declared, "theme", "light", asAdministrator: false, Maximum);

            await Store(writing).RecordAsync(preferences, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext clearing = database.Context())
        {
            PreferenceStore store = Store(clearing);
            PreferenceSet preferences = await store.FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken);
            preferences.Clear("theme");

            await store.RecordAsync(preferences, TestContext.Current.CancellationToken);
            await clearing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Empty((await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken)).Values);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC8: the declared values are in the database in the scheme's own
    /// shape, so a dump without the key-encryption key yields no preference.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC8_TheDeclaredValuesAreNotInTheDatabaseInPlainAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using (StoreContext writing = database.Context())
        {
            var preferences = PreferenceSet.Empty(subject);
            preferences.Set(Declared, "theme", "light", asAdministrator: false, Maximum);

            await Store(writing).RecordAsync(preferences, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        PreferenceRecord stored = await reading.AccountPreferences
            .SingleAsync(held => held.Subject == subject, TestContext.Current.CancellationToken);

        Assert.NotNull(stored.Values);
        Assert.Equal(PersonalDataFormat.Marker, stored.Values[0]);
        Assert.Equal(-1, stored.Values.AsSpan().IndexOf(Encoding.UTF8.GetBytes("theme")));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC9: once the subject's key is erased the declared values are
    /// unreadable, and the read says so rather than yielding what it finds.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC9_TheValuesOfAnErasedSubjectAreNotReadableAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using (StoreContext writing = database.Context())
        {
            var preferences = PreferenceSet.Empty(subject);
            preferences.SetLanguage("ar-EG");
            preferences.Set(Declared, "theme", "light", asAdministrator: false, Maximum);

            await Store(writing).RecordAsync(preferences, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await _deployment.EraseAsync(subject);

        await using StoreContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await Store(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The language and the time zone outlive an erasure, because a notice about a
    /// closed account still has to reach the person in a language they read.
    /// </summary>
    [Fact]
    public async Task FindBySubjectAsync_AnErasedSubjectWithNoValues_StillReadsItsLanguageAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using (StoreContext writing = database.Context())
        {
            var preferences = PreferenceSet.Empty(subject);
            preferences.SetLanguage("ar-EG");
            preferences.SetTimeZone("Africa/Cairo");

            await Store(writing).RecordAsync(preferences, TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await _deployment.EraseAsync(subject);

        await using StoreContext reading = database.Context();
        PreferenceSet read = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.Equal("ar-EG", read.Language);
        Assert.Equal("Africa/Cairo", read.TimeZone);
        Assert.Empty(read.Values);
    }

    /// <summary>
    /// Preferences recorded for a subject that has no key are a fault in the caller.
    /// </summary>
    [Fact]
    public async Task RecordAsync_ASubjectWithNoKey_ThrowsAsync()
    {
        await using StoreContext context = database.Context();

        var preferences = PreferenceSet.Empty(new SubjectId(Guid.NewGuid()));
        preferences.Set(Declared, "theme", "light", asAdministrator: false, Maximum);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store(context).RecordAsync(preferences, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static int Maximum => 8192;

    private static PreferenceDeclarations Declared { get; } = PreferenceDeclarations.Of(
    [
        new PreferenceDeclaration(
            "theme",
            PreferenceKind.Enum,
            "dark",
            Choices: new HashSet<string> { "dark", "light" }),
        new PreferenceDeclaration("text-size", PreferenceKind.Integer, "16"),
    ]);

    private PreferenceStore Store(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);
}
