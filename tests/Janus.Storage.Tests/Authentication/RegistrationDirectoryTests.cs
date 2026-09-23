using System;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Storage.Authentication.Registration;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Preferences;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.SubjectKeys;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What registration writes through the account directory, as the database keeps it
/// (REG-SESS-007, IDN-ATTR-001).
/// </summary>
/// <remarks>The port implementations are tested against the real database (D-156).</remarks>
[Trait("kind", "integration")]
public sealed class RegistrationDirectoryTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// IDN-ATTR-001: the account a registration creates keeps the language the
    /// registration found, written with the account, and its holder is told in it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_ATTR_001_TheAccountKeepsTheLanguageItsRegistrationFoundAsync()
    {
        SubjectId settled = Subjects.New();
        SubjectId unsettled = Subjects.New();

        await using (StoreContext writing = database.Context())
        {
            await Directory(writing).CreateAsync(Created(settled, "ar"), TestContext.Current.CancellationToken);
            await Directory(writing).CreateAsync(Created(unsettled, language: null), TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal("ar", await Directory(reading).LanguageAsync(settled, TestContext.Current.CancellationToken));
        Assert.Null(await Directory(reading).LanguageAsync(unsettled, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static NewAccount Created(SubjectId subject, string? language) =>
        new(
            subject,
            Noon,
            Identifiers: [],
            DateOfBirth: null,
            AdultAffirmed: true,
            Group: null,
            AnsweredAgeAt: Noon,
            TermsVersion: "1",
            NoticeVersion: "1",
            EmailMaximum: 5,
            PhoneMaximum: 5,
            language);

    private RegistrationDirectory Directory(StoreContext context) =>
        new(
            context,
            new AccountStore(context),
            new IdentifierStore(context, _deployment.Keys, Deployment.FingerprintKey, _deployment.Randomness),
            new ProfileStore(context, _deployment.Keys, _deployment.Randomness),
            new SubjectKeyStore(context, _deployment.Keys, _deployment.Randomness),
            new PreferenceStore(context, _deployment.Keys, _deployment.Randomness));
}
