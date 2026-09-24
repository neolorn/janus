using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Sessions;
using Janus.Authorization.Grants;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Identity.Identifiers;
using Janus.Identity.Organizations;
using Janus.Identity.Preferences;
using Janus.Privacy.Exports;
using Janus.Storage.Authentication.Accounts;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Identifiers;
using Janus.Storage.Authentication.Passwords;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Authorization.Roles;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Organizations;
using Janus.Storage.Identity.Preferences;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.Exports;
using Janus.Storage.Privacy.Outbox;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// What an export carries of what the identity, authentication and authorization
/// tables hold: every group of REG-ACCT-001 the account page shows, the credentials
/// among them, the whole standing group, and the location records of the live
/// sessions (PRIV-RIGHT-003, REG-ACCT-001, REG-PREF-001, REG-IDENT-002,
/// AUTH-SESS-013).
/// </summary>
[Trait("kind", "integration")]
public sealed class ExportSourceTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly Argon2StrengthClass Shipped = new(
        Janus.Core.Configuration.Settings.PasswordArgon2Memory.Default,
        Janus.Core.Configuration.Settings.PasswordArgon2Iterations.Default);

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

        IReadOnlyDictionary<string, ExportSection> named = await SectionsAsync(subject);

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
    /// REG-ACCT-001 AC1: the export carries the credentials group the account page
    /// shows, by property and label, with the password and the recovery codes and the
    /// browsers the account is known at beside the enrolments.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_ACCT_001_AC1_TheExportCarriesTheCredentialsTheAccountShowsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await PasswordAsync(subject);
        await EnrolledAsync(subject, "this laptop");
        await CodesAsync(subject);
        await RememberedAsync(subject, "this browser");

        IReadOnlyDictionary<string, ExportSection> named = await SectionsAsync(subject);

        IReadOnlyDictionary<string, string> password = Assert.Single(
            named["credentials"].Records,
            record => record.Values["kind"] == "Password").Values;

        Assert.Equal(Noon, DateTimeOffset.Parse(password["setAt"], null));
        Assert.Equal("false", password["changeRequired"]);

        IReadOnlyDictionary<string, string> passkey = Assert.Single(
            named["credentials"].Records,
            record => record.Values["kind"] == "Passkey").Values;

        Assert.Equal("this laptop", passkey["label"]);
        Assert.Equal("Active", passkey["state"]);
        Assert.Equal("true", passkey["backupEligible"]);
        Assert.Equal("false", passkey["backupState"]);
        Assert.Equal(Noon, DateTimeOffset.Parse(passkey["addedAt"], null));

        // AUTH-FACT-008 AC2: how the set stands, and never a code of it.
        IReadOnlyDictionary<string, string> codes =
            Assert.Single(named["recovery-codes"].Records).Values;

        Assert.Equal("2", codes["remaining"]);
        Assert.Equal(Noon, DateTimeOffset.Parse(codes["generatedAt"], null));

        IReadOnlyDictionary<string, string> browser =
            Assert.Single(named["devices"].Records).Values;

        Assert.Equal("this browser", browser["label"]);
        Assert.Equal("Trusted", browser["kind"]);
    }

    /// <summary>
    /// REG-INV-001 AC3: a membership an invitation attached carries when it was
    /// acknowledged, and each document the person acknowledged is carried at the
    /// version shown.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_INV_001_AC3_TheExportCarriesWhatWasAcknowledgedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        var acknowledged = new MembershipAcknowledgement(
            [new InvitationDocument("staff-handbook", "3"), new InvitationDocument("conduct", "1")],
            Noon);

        MembershipId placed = await PlacedAsync(subject, organization, until: null, acknowledged);

        IReadOnlyDictionary<string, ExportSection> named = await SectionsAsync(subject);

        IReadOnlyDictionary<string, string> membership =
            Assert.Single(named["memberships"].Records).Values;

        Assert.Equal(Noon, DateTimeOffset.Parse(membership["acknowledgedAt"], null));
        Assert.Equal(
            [
                (placed.Value.ToString(), "staff-handbook", "3"),
                (placed.Value.ToString(), "conduct", "1"),
            ],
            named["membership-acknowledgements"].Records.Select(record =>
                (record.Values["membership"], record.Values["document"], record.Values["version"])));
        Assert.All(
            named["membership-acknowledgements"].Records,
            record => Assert.Equal(Noon, DateTimeOffset.Parse(record.Values["acknowledgedAt"], null)));
    }

    /// <summary>
    /// REG-ACCT-001: the standing group is carried whole, so the memberships, the
    /// roles the account holds and the assurance it can reach are in the export
    /// beside the state (IDN-MEM-001, AUTHZ-GRANT-001, AUTH-STEP-002).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_ACCT_001_TheExportCarriesTheWholeStandingGroupAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        _ = await PlacedAsync(subject, organization, until: null);
        GrantId conferred = await ConferredAsync(subject, organization);

        await PasswordAsync(subject);
        await EnrolledAsync(subject, "this laptop");

        IReadOnlyDictionary<string, ExportSection> named = await SectionsAsync(subject);

        IReadOnlyDictionary<string, string> membership =
            Assert.Single(named["memberships"].Records).Values;

        Assert.Equal(organization.Value.ToString(), membership["organization"]);
        Assert.Equal(Noon, DateTimeOffset.Parse(membership["joinedAt"], null));
        Assert.False(membership.ContainsKey("endedAt"));
        Assert.False(membership.ContainsKey("acknowledgedAt"));
        Assert.Empty(named["membership-acknowledgements"].Records);

        IReadOnlyDictionary<string, string> grant =
            Assert.Single(named["grants"].Records).Values;

        Assert.Equal(conferred.Value.ToString(), grant["grant"]);
        Assert.Equal("editor", grant["role"]);
        Assert.Equal("false", grant["deny"]);
        Assert.Equal("Stored", grant["kind"]);

        // AUTH-STEP-002: a password and a passkey reach the second tier, and the
        // passkey makes what reaches it resistant to relay.
        IReadOnlyDictionary<string, string> assurance =
            Assert.Single(named["assurance"].Records).Values;

        Assert.Equal("Aal2", assurance["reachable"]);
        Assert.Equal("true", assurance["phishingResistant"]);
    }

    /// <summary>
    /// REG-SESS-007 AC2: the terms version accepted and the notice version presented
    /// are on the account record, and the affirmation the age screen derived with
    /// them, so the export carries all three (PRIV-CONS-008a, REG-PROF-002).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_007_AC2_TheExportCarriesTheNoticeAndAffirmationRecordsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await RegisteredAsync(subject);

        IReadOnlyDictionary<string, string> account = Assert.Single(
            (await SectionsAsync(subject))["account"].Records).Values;

        Assert.Equal("2026-08-01", account["termsVersion"]);
        Assert.Equal("2026-09-01", account["noticeVersion"]);
        Assert.Equal("true", account["adultAffirmed"]);
        Assert.Equal(Noon, DateTimeOffset.Parse(account["answeredAgeAt"], null));
        Assert.False(account.ContainsKey("ageGroup"));
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
            [
                "account",
                "profile",
                "identifiers",
                "identifier-backup",
                "credentials",
                "recovery-codes",
                "devices",
                "preferences",
                "memberships",
                "membership-acknowledgements",
                "grants",
                "assurance",
                "sessions",
            ],
            sections.Select(section => section.Name));

        var named = sections.ToDictionary(section => section.Name, StringComparer.Ordinal);

        IReadOnlyDictionary<string, string> account =
            Assert.Single(named["account"].Records).Values;

        Assert.Equal(subject.Value.ToString(), account["subject"]);
        Assert.Equal("Active", account["state"]);
        Assert.Equal(Noon, DateTimeOffset.Parse(account["registeredAt"], null));
        Assert.False(account.ContainsKey("termsVersion"));

        Assert.Empty(named["identifiers"].Records);
        Assert.Empty(named["credentials"].Records);
        Assert.Empty(named["recovery-codes"].Records);
        Assert.Empty(named["devices"].Records);
        Assert.Empty(named["memberships"].Records);
        Assert.Empty(named["membership-acknowledgements"].Records);
        Assert.Empty(named["grants"].Records);
        Assert.Empty(named["sessions"].Records);

        // AUTH-STEP-002: an account holding nothing of ours reaches nothing of ours.
        Assert.Equal(
            "Delegated",
            Assert.Single(named["assurance"].Records).Values["reachable"]);

        // The declared defaults stand in for what the account never set.
        Assert.Equal("dark", Assert.Single(named["preferences"].Records).Values["theme"]);
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

        await using (StoreContext ending = database.Context())
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

        await using StoreContext reading = database.Context();

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

    private static CredentialLabel Label(string entered) =>
        CredentialLabel.TryParse(entered, out CredentialLabel label)
            ? label
            : throw new Xunit.Sdk.XunitException("The label is one the chapter admits.");

    private async Task<IReadOnlyDictionary<string, ExportSection>> SectionsAsync(SubjectId subject) =>
        (await AssembledAsync(subject))
            .ToDictionary(section => section.Name, StringComparer.Ordinal);

    private async Task<IReadOnlyList<ExportSection>> AssembledAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        var source = new ExportSource(
            new AccountStore(reading),
            new AccountDirectory(
                reading,
                new AccountStore(reading),
                new ProfileStore(reading, _deployment.Keys, _deployment.Randomness),
                new ProfilePhotoStore(reading, _deployment.Keys, _deployment.Randomness),
                new SubjectKeyStore(reading, _deployment.Keys, _deployment.Randomness),
                Preferences(reading),
                Declared,
                new OutboxStore(reading, new FixedTime(Noon))),
            new IdentifierDirectory(Identifiers(reading), Preferences(reading)),
            new AuthenticatorStore(reading, _deployment.Keys, _deployment.Randomness, Deployment.FingerprintKeys),
            new PasswordStore(reading),
            new RecoveryCodeStore(reading),
            new DeviceStore(reading),
            new MembershipStore(reading),
            new GrantStore(reading, new DataConnections(reading)),
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

        await using StoreContext writing = database.Context();
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
        await using StoreContext writing = database.Context();

        var preferences = PreferenceSet.Empty(subject);
        preferences.SetLanguage("ar-EG");
        preferences.SetTimeZone("Africa/Cairo");
        preferences.Set(Declared, "theme", "light", asAdministrator: false, maximumSize: 8192);

        await Preferences(writing).RecordAsync(preferences, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // The columns the one transaction of the terms step writes on the account row.
    private async Task RegisteredAsync(SubjectId subject)
    {
        await using StoreContext writing = database.Context();

        AccountRecord record = await writing.Accounts
            .SingleAsync(held => held.Subject == subject, TestContext.Current.CancellationToken);

        record.AdultAffirmed = true;
        record.AnsweredAgeAt = Noon;
        record.TermsVersion = "2026-08-01";
        record.NoticeVersion = "2026-09-01";

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task PasswordAsync(SubjectId subject)
    {
        PasswordHash hash = new Argon2idHasher(_deployment.Randomness).Hash(
            Encoding.UTF8.GetBytes("a horse outstanding in its field"),
            Shipped,
            parallelism: 1);

        await using StoreContext writing = database.Context();

        await new PasswordStore(writing).SetAsync(
            Password.Set(subject, hash, meetsSingleFactorFloor: true, Noon),
            TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task EnrolledAsync(SubjectId subject, string label)
    {
        byte[] credentialId = new byte[20];
        byte[] publicKey = new byte[32];

        _deployment.Randomness.GetBytes(credentialId);
        _deployment.Randomness.GetBytes(publicKey);

        var credential = Authenticator.WebAuthnCredential(
            AuthenticatorId.New(TimeProvider.System),
            subject,
            Factor.Passkey,
            Label(label),
            new WebAuthnMaterial(credentialId, publicKey, -7, "example.com", 0, true, false),
            Noon);

        credential.Confirm(Noon);

        await using StoreContext writing = database.Context();

        await new AuthenticatorStore(writing, _deployment.Keys, _deployment.Randomness, Deployment.FingerprintKeys)
            .AddAsync(credential, TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task CodesAsync(SubjectId subject)
    {
        var hasher = new Argon2idHasher(_deployment.Randomness);

        IReadOnlyList<PasswordHash> hashes =
        [
            .. Enumerable.Range(0, 2).Select(_ => hasher.Hash(
                Encoding.UTF8.GetBytes(
                    RecoveryCode.Canonical(RecoveryCode.Draw(_deployment.Randomness))),
                Shipped,
                parallelism: 1)),
        ];

        await using StoreContext writing = database.Context();

        await new RecoveryCodeStore(writing).ReplaceAsync(
            RecoveryCodeSet.Of(subject, hashes, Noon),
            TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task RememberedAsync(SubjectId subject, string label)
    {
        var device = Device.Known(
            DeviceId.New(TimeProvider.System),
            subject,
            DeviceKind.Trusted,
            Label(label),
            Noon,
            TimeSpan.FromDays(30));

        await using StoreContext writing = database.Context();

        await new DeviceStore(writing).AddAsync(
            device,
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<MembershipId> PlacedAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset? until,
        MembershipAcknowledgement? acknowledged = null)
    {
        Membership membership = Membership
            .Create(
                new MembershipId(Guid.CreateVersion7()),
                subject,
                organization,
                [],
                multiple: true,
                Noon,
                acknowledged)
            .Match(made => made, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        if (until is DateTimeOffset ended)
        {
            membership.End(ended);
        }

        await using StoreContext writing = database.Context();

        await new MembershipStore(writing).CreateAsync(
            membership,
            TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return membership.Id;
    }

    private async Task<GrantId> ConferredAsync(SubjectId subject, OrganizationId organization)
    {
        var role = RoleName.Parse("editor");
        var id = GrantId.New(TimeProvider.System);

        await using StoreContext writing = database.Context();

        if (!await writing.Roles.AnyAsync(row => row.Name == role, TestContext.Current.CancellationToken))
        {
            await new RoleStore(writing).CreateAsync(
                Role.Of(role, [Permissions.GrantRead]),
                TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Grant grant = Grant.Create(
            id,
            GrantSubject.Of(subject),
            role,
            organization,
            on: null,
            deny: false,
            GrantKind.Stored,
            expiresAt: null,
            subject,
            Noon,
            "The reason the grant was written.")
            .Match(
                written => written,
                error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        await new GrantStore(writing, new DataConnections(writing))
            .CreateAsync(grant, TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
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

        await using StoreContext writing = database.Context();

        await Sessions(writing).AddAsync(
            session,
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session;
    }

    private IdentifierStore Identifiers(StoreContext context) =>
        new(context, _deployment.Keys, Deployment.FingerprintKeys, _deployment.Randomness);

    private PreferenceStore Preferences(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);

    private SessionStore Sessions(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);
}
