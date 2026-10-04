using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Accounts;

/// <summary>
/// The server half of the account application: what each list carries, what a change
/// to one of them touches, and what the policies and the declaration keep out of it
/// (FE-ACCT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccountApplicationTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Second = "other@example.test";

    private static readonly SessionLocation Somewhere = new("Cairo", "EG");

    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly PreferenceDeclarations Declared = PreferenceDeclarations.Of(
    [
        new PreferenceDeclaration("theme", PreferenceKind.String, "system"),
    ]);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly Deployment _deployment = new(preferences: Declared);

    /// <summary>
    /// A deployment able to register a browser and to declare one preference.
    /// </summary>
    public AccountApplicationTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _deployment.DisposeAsync();

        _randomness.Dispose();
    }

    /// <summary>
    /// IDN-ACCT-007 AC2 (D-166), end to end: a restriction fulfilled through the queue
    /// ends the account's session; the account then signs in with its password, reads
    /// itself as restricted, is refused a change to its profile with
    /// <c>authz.restricted</c>, still exercises its rights with a request, and exports
    /// its data from the session it has just proved.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountSignsInReadsAndIsRefusedAChangeAsync()
    {
        // The new-device check would hold the sign-in for a code (AUTH-FACT-016), which
        // is not what this test is about.
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);
        _deployment.Configuration.Set(Settings.PrivacyCalendarTimeZone, "Africa/Cairo");

        Browser before = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.AccountStates.Hold(subject, AccountState.Active);
        _deployment.Administers(Company);
        _deployment.Gate.Grant(subject, Company, Permissions.PrivacyRequestManage);

        Answer asked = await before.SendAsync(
            "POST",
            "/privacy/requests",
            ("type", "restriction"),
            ("detail", "the recorded date of birth is disputed"));

        Assert.Equal(StatusCodes.Status202Accepted, asked.Status);

        Answer fulfilled = await before.SendAsync(
            "POST",
            "/admin/privacy/requests/" + asked.Json().GetProperty("requestId").GetGuid() + "/fulfil");
        Answer ended = await before.SendAsync("GET", "/account");

        var after = new Browser(_deployment);

        _ = await after.SendAsync("GET", "/auth/session");

        Answer began = await after.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer signedIn = await after.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));
        Answer read = await after.SendAsync("GET", "/account");
        Answer changed = await after.SendAsync("PUT", "/account/profile", ("displayName", "Someone Else"));
        Answer rectification = await after.SendAsync(
            "POST",
            "/privacy/requests",
            ("type", "rectification"),
            ("detail", "the recorded total is wrong"));
        Answer exported = await after.SendAsync("GET", "/privacy/export?format=machine");

        Assert.Equal(StatusCodes.Status202Accepted, asked.Status);
        Assert.Equal(StatusCodes.Status204NoContent, fulfilled.Status);
        Assert.Equal(StatusCodes.Status401Unauthorized, ended.Status);
        Assert.Equal("complete", signedIn.Text("status"));
        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("restricted", read.Text("state"));
        Assert.Equal(StatusCodes.Status403Forbidden, changed.Status);
        Assert.Equal(ErrorCodes.Restricted.ToString(), changed.Text("code"));
        Assert.Equal(StatusCodes.Status202Accepted, rectification.Status);
        Assert.Equal(StatusCodes.Status200OK, exported.Status);
    }

    /// <summary>
    /// FE-ACCT-001 AC1: a credential carries what the list shows and nothing more,
    /// and the label is the only part of it the person edits.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_ACCT_001_AC1_ACredentialShowsItsPropertiesAndOnlyItsLabelChangesAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        Guid credential = Enrolled(Registered());

        JsonElement held = Single(await browser.SendAsync("GET", "/account/credentials"));

        Assert.Equal("passkey", held.GetProperty("kind").GetString());
        Assert.Equal("This laptop", held.GetProperty("label").GetString());
        Assert.True(held.GetProperty("backupState").GetBoolean());
        Assert.NotEqual(default, held.GetProperty("addedAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, held.GetProperty("lastUsedAt").ValueKind);

        Answer relabelled = await browser.SendAsync(
            "PATCH",
            "/account/credentials/" + credential,
            ("label", "The one at home"));

        JsonElement after = Single(await browser.SendAsync("GET", "/account/credentials"));

        Assert.Equal(StatusCodes.Status204NoContent, relabelled.Status);
        Assert.Equal("The one at home", after.GetProperty("label").GetString());
        Assert.Equal(Without(held, "label"), Without(after, "label"));
    }

    /// <summary>
    /// FE-ACCT-001 AC2: one of the sessions is the one asking, each says where it was
    /// used no more finely than a city, and ending another leaves that one standing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_ACCT_001_AC2_OneSessionIsCurrentAndAnotherIsEndedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        Guid elsewhere = await OpenedAsync(Registered());

        JsonElement listed = (await browser.SendAsync("GET", "/account/sessions")).Json();

        Assert.Equal(2, listed.GetArrayLength());
        Assert.Equal(1, Current(listed));

        foreach (JsonElement session in listed.EnumerateArray())
        {
            // A location is a city and a country or it is absent: there is no field
            // on it that would place a person more closely than that.
            JsonElement located = session.GetProperty("location");

            Assert.True(
                located.ValueKind is JsonValueKind.Null
                || Named(located).SequenceEqual(["city", "country"]));

            Assert.Equal(["browser", "os"], Named(session.GetProperty("device")));
        }

        Answer ended = await browser.SendAsync("DELETE", "/account/sessions/" + elsewhere);

        JsonElement left = (await browser.SendAsync("GET", "/account/sessions")).Json();

        Assert.Equal(StatusCodes.Status204NoContent, ended.Status);
        Assert.Equal(1, left.GetArrayLength());
        Assert.True(left[0].GetProperty("current").GetBoolean());
    }

    /// <summary>
    /// CONV-DESIGN-002 AC3, D-166: another account's session or browser, named in the
    /// path, is answered as one that does not exist, with the body an identifier nobody
    /// holds is answered with, apart from the correlation identifier.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AC3_AnotherAccountsSessionOrBrowserReadsAsNoneAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        var another = SubjectId.New(_randomness);
        Guid theirSession = await OpenedAsync(another);
        Guid theirBrowser = await KnownAsync(another);

        Answer session = await browser.SendAsync("DELETE", "/account/sessions/" + theirSession);
        Answer noSession = await browser.SendAsync("DELETE", "/account/sessions/" + Guid.NewGuid());
        Answer device = await browser.SendAsync("DELETE", "/account/devices/" + theirBrowser);
        Answer noDevice = await browser.SendAsync("DELETE", "/account/devices/" + Guid.NewGuid());

        Assert.All(
            new[] { session, noSession, device, noDevice },
            answer =>
            {
                Assert.Equal(StatusCodes.Status404NotFound, answer.Status);
                Assert.Equal(ErrorCodes.ResourceNotFound.ToString(), answer.Text("code"));
            });
        Assert.Equal(Without(noSession), Without(session));
        Assert.Equal(Without(noDevice), Without(device));
    }

    /// <summary>
    /// FE-ACCT-001 AC3: an identifier says whether it is primary, verified and
    /// locked; a removal takes it out of the list at once and the undo the notice
    /// carries puts it back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_ACCT_001_AC3_AnIdentifierIsRemovedAndTheUndoRestoresItAsync()
    {
        _deployment.Templates.Set(
            MessageKind.IdentifierRemoved,
            SendKind.Email,
            "en",
            new MessageTemplate("removed", "{link}"));

        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = Registered();
        Guid going = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, Second).Value;

        JsonElement emails = Emails(await browser.SendAsync("GET", "/account"));

        Assert.Equal(2, emails.GetArrayLength());
        Assert.Equal(1, Primary(emails));
        Assert.All(
            Values(emails),
            each => Assert.Equal(["id", "locked", "primary", "value", "verified"], each));

        Answer removed = await browser.SendAsync("DELETE", "/account/identifiers/" + going);

        Assert.Equal(StatusCodes.Status204NoContent, removed.Status);
        Assert.Equal(1, Emails(await browser.SendAsync("GET", "/account")).GetArrayLength());

        Answer restored = await browser.SendAsync(
            "POST",
            "/account/identifiers/" + going + "/undo",
            ("linkToken", Undo()));

        Assert.Equal(StatusCodes.Status204NoContent, restored.Status);
        Assert.Equal(2, Emails(await browser.SendAsync("GET", "/account")).GetArrayLength());
    }

    /// <summary>
    /// REG-IDENT-004 AC5 and AC2: an add is listed on the account as an unverified
    /// identifier and writes none until its code verifies it, and the verified
    /// identifier keeps the identifier it was listed under.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_004_AC5_AnAddIsListedUnverifiedUnderTheIdentifierItKeepsAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = Registered();

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("POST", "/account/identifiers", ("kind", "email"), ("value", Second))).Status);

        string staged = Assert.Single(_deployment.Pending.All).Identifier.Value.ToString();
        JsonElement listed = Listed(Emails(await browser.SendAsync("GET", "/account")), staged);

        Assert.Equal(Second, listed.GetProperty("value").GetString());
        Assert.False(listed.GetProperty("verified").GetBoolean());
        Assert.False(listed.GetProperty("primary").GetBoolean());
        Assert.Null(await _deployment.Identifiers.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));

        Answer verified = await browser.SendAsync(
            "POST",
            "/account/identifiers/" + staged + "/verify",
            ("code", Flow.Code(_deployment, IdentifierKind.Email)));

        Assert.Equal(StatusCodes.Status204NoContent, verified.Status);
        Assert.True(Listed(Emails(await browser.SendAsync("GET", "/account")), staged)
            .GetProperty("verified")
            .GetBoolean());
        Assert.Empty(_deployment.Pending.All);
        Assert.Equal(subject, await _deployment.Identifiers.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// API-CONV-005 AC1, REG-SESS-005 AC5: an add of a value another account holds is
    /// answered in the bytes an add of a value no account holds is, is listed alike, and
    /// a code presented for it is answered <c>auth.code.invalid</c> as a wrong code is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_005_AC1_AnAddOfAHeldValueIsAnsweredAndListedAsAFreshOneIsAsync()
    {
        const string held = "held@example.test";

        Browser browser = await Flow.SignedInAsync(_deployment);
        var other = SubjectId.New(_randomness);

        _ = _deployment.Identifiers.Verified(other, IdentifierKind.Email, held);

        Answer fresh = await browser.SendAsync("POST", "/account/identifiers", ("kind", "email"), ("value", Second));
        Answer duplicate = await browser.SendAsync("POST", "/account/identifiers", ("kind", "email"), ("value", held));

        Assert.Equal(StatusCodes.Status202Accepted, fresh.Status);
        Assert.Equal(fresh.Status, duplicate.Status);
        Assert.Equal(fresh.Body, duplicate.Body);

        string staged = _deployment.Pending.All
            .Single(pending => string.Equals(pending.Staged.Canonical, held, StringComparison.Ordinal))
            .Identifier
            .Value
            .ToString();
        JsonElement emails = Emails(await browser.SendAsync("GET", "/account"));

        Assert.Equal(3, emails.GetArrayLength());
        Assert.False(Listed(emails, staged).GetProperty("verified").GetBoolean());

        Answer presented = await browser.SendAsync(
            "POST",
            "/account/identifiers/" + staged + "/verify",
            ("code", "000000"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, presented.Status);
        Assert.Equal(ErrorCodes.CodeInvalid.ToString(), presented.Text("code"));
        Assert.Equal(other, await _deployment.Identifiers.OwnerAsync(
            IdentifierKind.Email,
            held,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-004 AC7: the right code for an add whose value another account has
    /// come to hold is answered 422 <c>auth.code.expired</c>, writes no identifier and
    /// leaves the add listed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_004_AC7_TheCodeOfAnAddWhoseValueIsHeldSinceIsAnsweredExpiredAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        var other = SubjectId.New(_randomness);

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("POST", "/account/identifiers", ("kind", "email"), ("value", Second))).Status);

        string staged = Assert.Single(_deployment.Pending.All).Identifier.Value.ToString();
        string code = Flow.Code(_deployment, IdentifierKind.Email);

        _ = _deployment.Identifiers.Verified(other, IdentifierKind.Email, Second);

        Answer presented = await browser.SendAsync(
            "POST",
            "/account/identifiers/" + staged + "/verify",
            ("code", code));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, presented.Status);
        Assert.Equal(ErrorCodes.CodeExpired.ToString(), presented.Text("code"));
        Assert.False(Listed(Emails(await browser.SendAsync("GET", "/account")), staged)
            .GetProperty("verified")
            .GetBoolean());
        Assert.Equal(other, await _deployment.Identifiers.OwnerAsync(
            IdentifierKind.Email,
            Second,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-006 (D-187): a removal naming a pending add answers 204 and ends its
    /// pending verification, so the account lists it no longer; nothing is reserved and
    /// no undo is sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_006_ARemovalNamingAPendingAddEndsItsPendingVerificationAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("POST", "/account/identifiers", ("kind", "email"), ("value", Second))).Status);

        string staged = Assert.Single(_deployment.Pending.All).Identifier.Value.ToString();

        Assert.Equal(2, Emails(await browser.SendAsync("GET", "/account")).GetArrayLength());

        Answer removed = await browser.SendAsync("DELETE", "/account/identifiers/" + staged);

        Assert.Equal(StatusCodes.Status204NoContent, removed.Status);
        Assert.Equal(1, Emails(await browser.SendAsync("GET", "/account")).GetArrayLength());
        Assert.Empty(_deployment.Pending.All);
        Assert.Null(await _deployment.Identifiers.ReservedToAsync(
            IdentifierKind.Email,
            Second,
            _deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// BFF-SESS-004 AC1 and AC2, IDN-LIFE-008: removing a sign-in identifier is a
    /// privilege change, so the session that made it is given a new secret beside a
    /// new synchronizer token, the secret it held before resolves nothing, and the
    /// browser carries on under the new one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_004_AC2_RemovingAnIdentifierRotatesTheSessionAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = Registered();
        Guid going = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, Second).Value;

        string before = browser.Cookies[BrowserCookies.Session];
        string token = browser.Cookies[BrowserCookies.Csrf];

        Answer removed = await browser.SendAsync("DELETE", "/account/identifiers/" + going);

        Assert.Equal(StatusCodes.Status204NoContent, removed.Status);
        Assert.NotEqual(before, browser.Cookies[BrowserCookies.Session]);
        Assert.NotEqual(token, browser.Cookies[BrowserCookies.Csrf]);
        Assert.Null(await _deployment.Sessions.FindByFingerprintAsync(
            OpaqueToken.Of(before).Fingerprint(),
            TestContext.Current.CancellationToken));
        Assert.Equal(StatusCodes.Status200OK, (await browser.SendAsync("GET", "/account")).Status);
    }

    /// <summary>
    /// BFF-SESS-004 AC2, IDN-LIFE-008 AC1: a replacement staged in one session and
    /// completed by the code typed in another keeps the completing session, which
    /// answers to a new secret from then on, and ends the staging one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_004_AC2_TheSessionThatCompletesAReplacementAnswersToItsNewSecretAsync()
    {
        const string replaced = "replaced@example.test";

        _deployment.Configuration.Set(Settings.IdentifiersEmailMax, 1);

        Browser staging = await Flow.SignedInAsync(_deployment);
        SubjectId subject = Registered();
        Guid changing = (await _deployment.Identifiers.HeldAsync(subject, TestContext.Current.CancellationToken))
            .All
            .Single(held => held.Kind is IdentifierKind.Email)
            .Id
            .Value;

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await staging.SendAsync(
                "PUT",
                "/account/identifiers/" + changing + "/replace",
                ("value", replaced))).Status);

        var secret = OpaqueToken.Draw(_randomness);
        var token = OpaqueToken.Draw(_randomness);
        var completing = new Browser(_deployment);

        await _deployment.Sessions.AddAsync(
            Session.Begin(
                SessionId.New(_deployment.Clock),
                subject,
                new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
                new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")) { Location = Somewhere },
                _deployment.Clock.GetUtcNow(),
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(30),
                breakGlassReason: null),
            secret.Fingerprint(),
            token.Fingerprint(),
            TestContext.Current.CancellationToken);

        completing.Hold(BrowserCookies.Session, secret.Value);
        completing.Hold(BrowserCookies.Csrf, token.Value);

        Answer verified = await completing.SendAsync(
            "POST",
            "/account/identifiers/" + changing + "/verify",
            ("code", Flow.Code(_deployment, IdentifierKind.Email)));

        Assert.Equal(StatusCodes.Status204NoContent, verified.Status);
        Assert.NotEqual(secret.Value, completing.Cookies[BrowserCookies.Session]);
        Assert.Null(await _deployment.Sessions.FindByFingerprintAsync(
            secret.Fingerprint(),
            TestContext.Current.CancellationToken));
        Assert.Equal(StatusCodes.Status200OK, (await completing.SendAsync("GET", "/account")).Status);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await staging.SendAsync("GET", "/account")).Status);
    }

    /// <summary>
    /// FE-ACCT-001 AC4: the declaration decides what a preference set holds, so a key
    /// the host never declared is neither answered with nor taken.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_ACCT_001_AC4_AnUndeclaredKeyIsNeitherShownNorTakenAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer set = await browser.SendAsync(
            "PUT",
            "/account/preferences",
            ("declared", new Dictionary<string, string>(StringComparer.Ordinal) { ["theme"] = "dark" }));

        Assert.Equal(StatusCodes.Status204NoContent, set.Status);

        JsonElement preferences = (await browser.SendAsync("GET", "/account/preferences")).Json();

        Assert.Equal("dark", preferences.GetProperty("declared").GetProperty("theme").GetString());

        Answer undeclared = await browser.SendAsync(
            "PUT",
            "/account/preferences",
            ("declared", new Dictionary<string, string>(StringComparer.Ordinal) { ["density"] = "wide" }));

        Assert.Equal(ErrorCodes.PreferenceUndeclared.ToString(), undeclared.Text("code"));
        Assert.False(
            (await browser.SendAsync("GET", "/account/preferences"))
                .Json()
                .GetProperty("declared")
                .TryGetProperty("density", out _));
    }

    /// <summary>
    /// FE-ACCT-001 AC5: the two fields the policies gate appear only where they are
    /// switched on, and the date of birth is not the person's to change even then.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_ACCT_001_AC5_TheGatedProfileFieldsFollowTheirPoliciesAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        JsonElement off = Profile(await browser.SendAsync("GET", "/account"));

        Assert.Equal(JsonValueKind.Null, off.GetProperty("legalName").ValueKind);
        Assert.Equal(JsonValueKind.Null, off.GetProperty("dateOfBirth").ValueKind);

        Answer refused = await browser.SendAsync(
            "PUT",
            "/account/profile",
            ("legalName", "Someone Else"));

        Assert.Equal(ErrorCodes.ProfileNotAccepted.ToString(), refused.Text("code"));

        _deployment.Configuration.Set(
            Settings.ProfileLegalName,
            AttributeRequirement.Optional);

        Answer taken = await browser.SendAsync(
            "PUT",
            "/account/profile",
            ("legalName", "Someone Else"));

        Assert.Equal(StatusCodes.Status204NoContent, taken.Status);
        Assert.Equal(
            "Someone Else",
            Profile(await browser.SendAsync("GET", "/account")).GetProperty("legalName").GetString());

        Answer immutable = await browser.SendAsync(
            "PUT",
            "/account/profile",
            ("dateOfBirth", "1990-01-01"));

        Assert.Equal(ErrorCodes.ProfileNotAccepted.ToString(), immutable.Text("code"));
    }

    // The token the removal notice carried, which reaches the remaining set and not
    // the address that went (REG-IDENT-006).
    private string Undo()
    {
        foreach (MailMessage sent in _deployment.Mail.Taken)
        {
            if (string.Equals(sent.Subject, "removed", StringComparison.Ordinal))
            {
                return Landing.Token(sent.Body);
            }
        }

        throw new InvalidOperationException("No removal notice went out.");
    }

    // What an account already holds in the two fields the policies gate.
    private static HeldProfile Held(string legalName, DateOnly dateOfBirth)
    {
        if (!DisplayName.TryParse("Kestrel", out DisplayName shown)
            || !LegalName.TryParse(legalName, out LegalName named))
        {
            throw new InvalidOperationException("The profile does not parse.");
        }

        return new HeldProfile(shown, named, dateOfBirth, null);
    }

    // The account the flow registered, which the endpoints answer for.
    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the account committed after the gate step
    /// and before the first write refuses a change of the profile, of the preferences and
    /// of a credential's label, and each stays as it stood.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesEachSettingAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        Guid credential = Enrolled(Registered());
        string profile = Profile(await browser.SendAsync("GET", "/account")).GetRawText();
        string preferences = (await browser.SendAsync("GET", "/account/preferences")).Body;

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("PUT", "/account/profile", ("displayName", "Someone Else")));
        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync(
                "PUT",
                "/account/preferences",
                ("declared", new Dictionary<string, string>(StringComparer.Ordinal) { ["theme"] = "dark" })));
        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("PATCH", "/account/credentials/" + credential, ("label", "The one at home")));

        Assert.Equal(profile, Profile(await browser.SendAsync("GET", "/account")).GetRawText());
        Assert.Equal(preferences, (await browser.SendAsync("GET", "/account/preferences")).Body);
        Assert.Equal(
            "This laptop",
            Single(await browser.SendAsync("GET", "/account/credentials")).GetProperty("label").GetString());
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the account committed after the gate step
    /// and before the first write refuses an identifier's addition, verification,
    /// promotion, backup setting, removal and replacement, and the identifiers stay as
    /// they stood.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesEachIdentifierChangeAsync()
    {
        const string third = "third@example.test";

        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = Registered();
        Guid second = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, Second).Value;
        string held = Emails(await browser.SendAsync("GET", "/account")).GetRawText();

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("POST", "/account/identifiers", ("kind", "email"), ("value", third)));

        Assert.Equal(held, Emails(await browser.SendAsync("GET", "/account")).GetRawText());
        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("POST", "/account/identifiers", ("kind", "email"), ("value", third))).Status);

        HeldIdentifiers staged = await _deployment.Identifiers.HeldAsync(subject, TestContext.Current.CancellationToken);
        Guid added = staged.All.Single(each => !each.IsVerified).Id.Value;
        Guid primary = staged.All.Single(each => each.IsPrimary && each.Kind is IdentifierKind.Email).Id.Value;
        string code = Flow.Code(_deployment, IdentifierKind.Email);

        held = Emails(await browser.SendAsync("GET", "/account")).GetRawText();

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("POST", "/account/identifiers/" + added + "/verify", ("code", code)));
        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("POST", "/account/identifiers/" + second + "/primary"));
        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("PUT", "/account/identifiers/backup", ("kind", "email"), ("setting", "primary-only")));
        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("DELETE", "/account/identifiers/" + second));

        _deployment.Configuration.Set(Settings.IdentifiersEmailMax, 1);

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync(
                "PUT",
                "/account/identifiers/" + primary + "/replace",
                ("value", "replaced@example.test")));

        Assert.Equal(held, Emails(await browser.SendAsync("GET", "/account")).GetRawText());
        Assert.Equal(
            (await _deployment.Identifiers.HeldAsync(subject, TestContext.Current.CancellationToken))
                .NoticeSet
                .Select(each => each.Id),
            staged.NoticeSet.Select(each => each.Id));
    }

    private SubjectId Registered() => _deployment.Directory.Created[^1].Subject;

    // One credential on that account, of the kind the list shows most about.
    private Guid Enrolled(SubjectId subject)
    {
        var held = Authenticator.WebAuthnCredential(
            AuthenticatorId.New(_deployment.Clock),
            subject,
            Factor.Passkey,
            Labelled("This laptop"),
            new WebAuthnMaterial(
                new byte[] { 1, 2, 3 },
                new byte[] { 4, 5, 6 },
                Algorithm: -7,
                "identity.example.test",
                Counter: 0,
                BackupEligible: true,
                BackupState: true),
            _deployment.Clock.GetUtcNow());

        _deployment.Authenticators.Hold(held);

        return held.Id.Value;
    }

    // A second session on the same account, which is what a second device leaves.
    // The body without the correlation identifier, which differs between any two
    // requests by design (API-CONV-002 AC2).
    private static string Without(Answer answer) =>
        answer.Body.Replace(answer.Text("correlationId"), string.Empty, StringComparison.Ordinal);

    // A browser the account trusts.
    private async Task<Guid> KnownAsync(SubjectId subject)
    {
        var device = Device.Known(
            DeviceId.New(_deployment.Clock),
            subject,
            DeviceKind.Trusted,
            Labelled("Their laptop"),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(30));

        await using AsyncServiceScope scope = _deployment.Scope();

        await scope.ServiceProvider.GetRequiredService<IDeviceStore>().AddAsync(
            device,
            OpaqueToken.Draw(_randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        return device.Id.Value;
    }

    private async Task<Guid> OpenedAsync(SubjectId subject)
    {
        var id = SessionId.New(_deployment.Clock);

        await _deployment.Sessions.AddAsync(
            Session.Begin(
                id,
                subject,
                new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
                new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")) { Location = Somewhere },
                Noon,
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(30),
                breakGlassReason: null),
            OpaqueToken.Draw(_randomness).Fingerprint(),
            OpaqueToken.Draw(_randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        return id.Value;
    }

    private static CredentialLabel Labelled(string label) =>
        CredentialLabel.TryParse(label, out CredentialLabel named)
            ? named
            : throw new InvalidOperationException("The label does not parse.");

    private static JsonElement Single(Answer answer)
    {
        JsonElement listed = answer.Json();

        Assert.Equal(1, listed.GetArrayLength());

        return listed[0];
    }

    private static JsonElement Profile(Answer account) => account.Json().GetProperty("profile");

    private static JsonElement Emails(Answer account) =>
        account.Json().GetProperty("identifiers").GetProperty("emails");

    // The listed identifier an identifier names.
    private static JsonElement Listed(JsonElement emails, string id) =>
        emails.EnumerateArray().Single(each =>
            string.Equals(each.GetProperty("id").GetString(), id, StringComparison.OrdinalIgnoreCase));

    // How many of the listed sessions say they are the one asking.
    private static int Current(JsonElement listed)
    {
        int current = 0;

        foreach (JsonElement session in listed.EnumerateArray())
        {
            if (session.GetProperty("current").GetBoolean())
            {
                current++;
            }
        }

        return current;
    }

    // How many of the listed identifiers are the primary of their kind.
    private static int Primary(JsonElement listed)
    {
        int primary = 0;

        foreach (JsonElement identifier in listed.EnumerateArray())
        {
            if (identifier.GetProperty("primary").GetBoolean())
            {
                primary++;
            }
        }

        return primary;
    }

    private static List<string[]> Values(JsonElement listed)
    {
        var named = new List<string[]>();

        foreach (JsonElement identifier in listed.EnumerateArray())
        {
            named.Add(Named(identifier));
        }

        return named;
    }

    private static string[] Named(JsonElement held) =>
        [.. held.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];

    // The document with one field taken out, so two readings are compared on
    // everything the change was not about.
    private static string Without(JsonElement held, string field)
    {
        var kept = new List<string>();

        foreach (JsonProperty property in held.EnumerateObject())
        {
            if (!string.Equals(property.Name, field, StringComparison.Ordinal))
            {
                kept.Add(property.Name + "=" + property.Value.ToString());
            }
        }

        return string.Join("; ", kept);
    }

    /// <summary>
    /// IDN-ATTR-007 AC2: while the two keys are off, neither field is taken from a
    /// request and neither is carried in an answer, whatever the account holds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_007_AC2_TheSwitchedOffFieldsAreNeitherTakenNorCarriedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Accounts.Holds(Registered(), Held("Kestrel Ismail", new DateOnly(1990, 1, 1)));

        JsonElement profile = Profile(await browser.SendAsync("GET", "/account"));

        Assert.Equal(JsonValueKind.Null, profile.GetProperty("legalName").ValueKind);
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("dateOfBirth").ValueKind);

        Assert.Equal(
            ErrorCodes.ProfileNotAccepted.ToString(),
            (await browser.SendAsync("PUT", "/account/profile", ("legalName", "Someone Else"))).Text("code"));

        Assert.Equal(
            ErrorCodes.ProfileNotAccepted.ToString(),
            (await browser.SendAsync("PUT", "/account/profile", ("dateOfBirth", "1990-01-01"))).Text("code"));
    }
}
