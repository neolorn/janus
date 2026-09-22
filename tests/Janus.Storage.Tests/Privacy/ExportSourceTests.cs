using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Identity.Identifiers;
using Janus.Identity.Preferences;
using Janus.Privacy.Exports;
using Janus.Storage.Authentication.Accounts;
using Janus.Storage.Authentication.Identifiers;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Preferences;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.Exports;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// What an export carries of what the identity and authentication tables hold: the
/// preferences, the identifiers with their roles and state, and the location records
/// of the live sessions (PRIV-RIGHT-003, REG-PREF-001, REG-IDENT-002, AUTH-SESS-013).
/// </summary>
[Trait("kind", "integration")]
public sealed class ExportSourceTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly PreferenceDeclarations Declared = PreferenceDeclarations.Of(
    [
        new PreferenceDeclaration(
            "theme",
            PreferenceKind.Enum,
            "dark",
            Choices: new HashSet<string> { "dark", "light" }),
        new PreferenceDeclaration("text-size", PreferenceKind.Integer, "16"),
    ]);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// PRIV-RIGHT-003 AC3: the export of an account with declared preferences,
    /// several identifiers and a live session carries the preference values, every
    /// identifier with its role and state, and the session's location record.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AC3_TheExportCarriesThePreferencesIdentifiersAndSessionsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        string first = Fresh("Ahmed");
        string second = Fresh("Ahmed.Work");

        IdentifierId primary = await AddedAsync(subject, first, verified: true, primary: true);
        _ = await AddedAsync(subject, second, verified: false, primary: false);

        await SettledAsync(subject);
        await SignedInAsync(subject);

        IReadOnlyList<ExportSection> sections = await AssembledAsync(subject);

        var named = sections.ToDictionary(section => section.Name, StringComparer.Ordinal);

        // REG-PREF-001 AC4: the values in force, the declared default included.
        IReadOnlyDictionary<string, string> preferences =
            Assert.Single(named["preferences"].Records).Values;

        Assert.Equal("ar-EG", preferences["language"]);
        Assert.Equal("Africa/Cairo", preferences["timeZone"]);
        Assert.Equal("light", preferences["theme"]);
        Assert.Equal("16", preferences["text-size"]);

        // REG-IDENT-002: every identifier, with the role and the verification state.
        Assert.Equal(2, named["identifiers"].Records.Count);

        IReadOnlyDictionary<string, string> anchor = named["identifiers"].Records
            .Select(record => record.Values)
            .Single(values => values["value"] == first);

        Assert.Equal("Email", anchor["kind"]);
        Assert.Equal("true", anchor["verified"]);
        Assert.Equal("true", anchor["primary"]);
        Assert.Equal("true", anchor["securityNotice"]);

        IReadOnlyDictionary<string, string> spare = named["identifiers"].Records
            .Select(record => record.Values)
            .Single(values => values["value"] == second);

        Assert.Equal("false", spare["verified"]);
        Assert.Equal("false", spare["primary"]);

        Assert.Equal(
            primary.Value.ToString(),
            Assert.Single(
                named["identifier-backup"].Records,
                record => record.Values["kind"] == "Email").Values["named"]);

        // AUTH-SESS-013: the location resolved at sign-in and the one at last use.
        IReadOnlyDictionary<string, string> session =
            Assert.Single(named["sessions"].Records).Values;

        Assert.Equal("Firefox", session["browser"]);
        Assert.Equal("Linux", session["operatingSystem"]);
        Assert.Equal("Cairo", session["signedInCity"]);
        Assert.Equal("EG", session["signedInCountry"]);
        Assert.Equal("Cairo", session["lastUsedCity"]);
    }

    /// <summary>
    /// PRIV-RIGHT-003: an account that has settled nothing still exports, carrying
    /// the sections empty rather than answering with nothing at all.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AnAccountThatHasSettledNothingStillExportsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        IReadOnlyList<ExportSection> sections = await AssembledAsync(subject);

        Assert.Equal(
            ["account", "profile", "identifiers", "identifier-backup", "preferences", "sessions"],
            sections.Select(section => section.Name));

        IReadOnlyDictionary<string, string> account =
            Assert.Single(sections[0].Records).Values;

        Assert.Equal(subject.Value.ToString(), account["subject"]);
        Assert.Equal("Active", account["state"]);
        Assert.Equal(Noon, DateTimeOffset.Parse(account["registeredAt"], null));

        Assert.Empty(sections[2].Records);
        Assert.Empty(sections[5].Records);

        // The declared defaults stand in for what the account never set.
        Assert.Equal("dark", Assert.Single(sections[4].Records).Values["theme"]);
    }

    /// <summary>
    /// AUTH-SESS-013: an ended session is not a live one, so nothing of where it was
    /// used reaches the export.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_003_AnEndedSessionIsNotCarriedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Session session = await SignedInAsync(subject);

        await using (JanusDbContext ending = database.Context())
        {
            await Sessions(ending).EndSpineAsync(
                session.Id,
                Noon + TimeSpan.FromMinutes(1),
                TestContext.Current.CancellationToken);
        }

        IReadOnlyList<ExportSection> sections = await AssembledAsync(subject);

        Assert.Empty(Assert.Single(sections, section => section.Name == "sessions").Records);
    }

    /// <summary>
    /// REG-PREF-001 AC4: the export carries the preferences in force, and after an
    /// erasure the declared values are no longer readable while the language and the
    /// time zone, which are not under the subject key, still are.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_PREF_001_AC4_TheExportCarriesThePreferencesAndErasureTakesTheValuesAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        await SettledAsync(subject);

        IReadOnlyDictionary<string, string> exported = Assert.Single(
            Assert.Single(
                await AssembledAsync(subject),
                section => section.Name == "preferences").Records).Values;

        Assert.Equal("light", exported["theme"]);
        Assert.Equal("16", exported["text-size"]);

        await _deployment.EraseAsync(subject);

        await using JanusDbContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await Preferences(reading).FindBySubjectAsync(
                subject,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ATTR-001 AC1: the language the person set is in the subject access export
    /// beside the rest of what is held about them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_001_AC1_TheLanguagePreferenceIsInTheExportAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        await SettledAsync(subject);

        IReadOnlyDictionary<string, string> exported = Assert.Single(
            Assert.Single(
                await AssembledAsync(subject),
                section => section.Name == "preferences").Records).Values;

        Assert.Equal("ar-EG", exported["language"]);
        Assert.Equal("Africa/Cairo", exported["timeZone"]);
    }

    private static string Fresh(string person) =>
        person + "." + Guid.NewGuid().ToString("N") + "@Example.COM";

    private async Task<IReadOnlyList<ExportSection>> AssembledAsync(SubjectId subject)
    {
        await using JanusDbContext reading = database.Context();

        var source = new ExportSource(
            new AccountDirectory(
                new AccountStore(reading),
                new ProfileStore(reading, _deployment.Keys, _deployment.Randomness),
                new ProfilePhotoStore(reading, _deployment.Keys, _deployment.Randomness),
                Preferences(reading),
                Declared),
            new IdentifierDirectory(Identifiers(reading), Preferences(reading)),
            Sessions(reading),
            Declared,
            new FixedTime(Noon));

        return await source.SectionsAsync(subject, TestContext.Current.CancellationToken);
    }

    private async Task<IdentifierId> AddedAsync(
        SubjectId subject,
        string entered,
        bool verified,
        bool primary)
    {
        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

        var id = IdentifierId.New(TimeProvider.System);

        await using JanusDbContext writing = database.Context();
        IdentifierStore store = Identifiers(writing);

        IdentifierSet set = await store.FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        set.Add(Identifier.Email(id, subject, address, entered, Noon), maximum: 5);

        if (verified)
        {
            set.Verify(id, Noon);
        }

        if (primary)
        {
            set.MakePrimary(id);
            set.Backup(IdentifierKind.Email).UseNamed(id);
        }

        await store.RecordAsync(set, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task SettledAsync(SubjectId subject)
    {
        await using JanusDbContext writing = database.Context();

        var preferences = PreferenceSet.Empty(subject);
        preferences.SetLanguage("ar-EG");
        preferences.SetTimeZone("Africa/Cairo");
        preferences.Set(Declared, "theme", "light", asAdministrator: false, maximumSize: 8192);

        await Preferences(writing).RecordAsync(preferences, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Session> SignedInAsync(SubjectId subject)
    {
        var session = Session.Begin(
            SessionId.New(TimeProvider.System),
            subject,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")) { Location = new SessionLocation("Cairo", "EG") },
            Noon,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            satisfiesEveryGate: false);

        await using JanusDbContext writing = database.Context();

        await Sessions(writing).AddAsync(
            session,
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session;
    }

    private IdentifierStore Identifiers(JanusDbContext context) =>
        new(context, _deployment.Keys, Deployment.FingerprintKey, _deployment.Randomness);

    private PreferenceStore Preferences(JanusDbContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);

    private SessionStore Sessions(JanusDbContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);
}
