using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Records;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Accounts;

/// <summary>
/// The account endpoints of chapter 09 section 8a: an administrator suspends an account
/// and reactivates it, and a host reads another person's account (IDN-LIFE-013,
/// AUTH-SESS-010, IDN-READ-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccountAdministrationEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly PreferenceDeclarations Declared = PreferenceDeclarations.Of(
    [
        new PreferenceDeclaration("badge", PreferenceKind.Boolean, "false", AdministratorOnly: true),
    ]);

    private readonly Deployment _deployment = new(preferences: Declared);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public AccountAdministrationEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _randomness.Dispose();
        await _deployment.DisposeAsync();
    }

    /// <summary>
    /// AUTH-SESS-010 and IDN-LIFE-013 AC1: the suspension ends the account's session in
    /// the operation that suspends it, and the reactivation stands the account back up;
    /// without <c>account:manage</c> neither is done, a second reactivation is a conflict
    /// with the state, and a subject no account bears is not found.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_010_AnAccountIsSuspendedAndReactivatedAsync()
    {
        Session member = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);
        Answer withheld = await browser.SendAsync("POST", PathOf(member.Subject, "suspend"));

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.AccountManage);

        Answer suspended = await browser.SendAsync("POST", PathOf(member.Subject, "suspend"));
        AccountState? held = await StateAsync(member.Subject);
        Answer reactivated = await browser.SendAsync("POST", PathOf(member.Subject, "reactivate"));
        Answer again = await browser.SendAsync("POST", PathOf(member.Subject, "reactivate"));
        Answer unknown = await browser.SendAsync("POST", PathOf(SubjectId.New(_randomness), "reactivate"));

        Assert.Equal(StatusCodes.Status403Forbidden, withheld.Status);
        Assert.Equal(StatusCodes.Status204NoContent, suspended.Status);
        Assert.Equal(AccountState.Suspended, held);
        Assert.NotNull(member.EndedAt);
        Assert.Equal(StatusCodes.Status204NoContent, reactivated.Status);
        Assert.Equal(AccountState.Active, await StateAsync(member.Subject));
        Assert.Equal(StatusCodes.Status409Conflict, again.Status);
        Assert.Equal("identity.account.stateconflict", again.Text("code"));
        Assert.Equal("active", again.Json().GetProperty("details").GetProperty("state").GetString());
        Assert.Equal(StatusCodes.Status404NotFound, unknown.Status);
        Assert.Equal("identity.account.notfound", unknown.Text("code"));
    }

    /// <summary>
    /// CONV-DESIGN-004 AC2: a subject is bound at the edge as the identifier it is, so a
    /// route naming the max UUID, which no subject is issued, is malformed and reaches
    /// no operation.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_004_AC2_ARouteNamingTheMaxUuidAsASubjectIsMalformedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.AccountManage);

        Answer refused = await browser.SendAsync("POST", "/admin/accounts/ffffffff-ffff-ffff-ffff-ffffffffffff/suspend");

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), refused.Text("code"));
    }

    /// <summary>
    /// PRIV-RIGHT-004 AC2 (D-166): the lift is stepped up, so a session that has proved
    /// itself makes a restricted account active, a second finds no restriction to lift,
    /// which is a conflict with the state, and once the proof is no longer recent a
    /// restricted account is refused with the step-up code and stays restricted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_004_AC2_ARestrictionIsLiftedAsync()
    {
        Session member = await MemberAsync();
        Session later = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Accounts.Stands(member.Subject, AccountState.Restricted);
        _deployment.Accounts.Stands(later.Subject, AccountState.Restricted);
        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.AccountManage);

        Answer lifted = await browser.SendAsync("POST", PathOf(member.Subject, "restriction/lift"));
        Answer again = await browser.SendAsync("POST", PathOf(member.Subject, "restriction/lift"));

        _deployment.Clock.Advance(Settings.SessionStepUpRecency.Default + TimeSpan.FromMinutes(1));

        Answer stale = await browser.SendAsync("POST", PathOf(later.Subject, "restriction/lift"));

        Assert.Equal(StatusCodes.Status204NoContent, lifted.Status);
        Assert.Equal(AccountState.Active, await StateAsync(member.Subject));
        Assert.Equal(StatusCodes.Status409Conflict, again.Status);
        Assert.Equal("identity.account.stateconflict", again.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, stale.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), stale.Text("code"));
        Assert.Equal(AccountState.Restricted, await StateAsync(later.Subject));
    }

    /// <summary>
    /// IDN-LIFE-003: a deletion is cancelled on the subject's behalf, and one a takedown
    /// began is refused as a conflict naming the takedown.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_ADeletionIsCancelledOnTheSubjectsBehalfAsync()
    {
        Session member = await MemberAsync();
        Session takenDown = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);
        DateTimeOffset since = _deployment.Clock.GetUtcNow().AddDays(-1);

        _deployment.Accounts.Deleting(member.Subject, DeletionOrigin.Self, since);
        _deployment.Accounts.Deleting(takenDown.Subject, DeletionOrigin.Takedown, since);
        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.AccountManage);

        Answer cancelled = await browser.SendAsync("POST", PathOf(member.Subject, "delete/cancel"));
        Answer refused = await browser.SendAsync("POST", PathOf(takenDown.Subject, "delete/cancel"));

        Assert.Equal(StatusCodes.Status204NoContent, cancelled.Status);
        Assert.Equal(AccountState.Active, await StateAsync(member.Subject));
        Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
        Assert.Equal("identity.takedown.active", refused.Text("code"));
    }

    /// <summary>
    /// IDN-ATTR-003 AC3: an account's photo is served to an administrator as the JPEG it
    /// is, with nothing a shared cache could hand to anyone else, and an account that
    /// shows none is not found.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_003_AC3_AnAccountsPhotoIsServedToAnAdministratorAsync()
    {
        Session member = await MemberAsync();
        Session bare = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);
        var organization = OrganizationId.New(_deployment.Clock);

        _deployment.Memberships.Place(member.Subject, organization);
        _deployment.Memberships.Place(bare.Subject, organization);
        _deployment.Configuration.Set(Settings.PolicyDefault, Policies.SystemDefault with { Photos = true });
        _deployment.Accounts.Shows(member.Subject, Encoding.ASCII.GetBytes("a-photo"));
        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.AccountManage);

        Answer read = await browser.SendAsync("GET", PathOf(member.Subject, "photo"));
        Answer none = await browser.SendAsync("GET", PathOf(bare.Subject, "photo"));

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("image/jpeg", read.Header("Content-Type"));
        Assert.Equal("no-store", read.Header("Cache-Control"));
        Assert.Null(read.Header("ETag"));
        Assert.Equal("a-photo", read.Body);
        Assert.Equal(StatusCodes.Status404NotFound, none.Status);
    }

    /// <summary>
    /// IDN-ATTR-003 AC3 (D-166): an account that shows no photo, and one whose
    /// organization's policy withholds photos, are answered alike with <c>identity.photo.notfound</c>
    /// in the body every refusal carries.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_003_AC3_AnAccountWithoutAPhotoIsAnsweredWithTheCodeAsync()
    {
        Session bare = await MemberAsync();
        Session withheld = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);
        var showing = OrganizationId.New(_deployment.Clock);
        var hiding = OrganizationId.New(_deployment.Clock);

        _deployment.Memberships.Place(bare.Subject, showing);
        _deployment.Memberships.Place(withheld.Subject, hiding);
        _deployment.Configuration.Set(Settings.PolicyDefault, Policies.SystemDefault with { Photos = true });
        _deployment.Configuration.Set(
            Settings.OrganizationPolicy,
            hiding.ToString(),
            PolicyOverride.None with { Photos = false });
        _deployment.Accounts.Shows(withheld.Subject, Encoding.ASCII.GetBytes("a-photo"));
        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.AccountManage);

        Answer none = await browser.SendAsync("GET", PathOf(bare.Subject, "photo"));
        Answer policy = await browser.SendAsync("GET", PathOf(withheld.Subject, "photo"));

        foreach (Answer answer in new[] { none, policy })
        {
            Assert.Equal(StatusCodes.Status404NotFound, answer.Status);
            Assert.Equal(ErrorCodes.PhotoNotFound.ToString(), answer.Text("code"));
            Assert.Empty(answer.Json().GetProperty("details").EnumerateObject());
        }
    }

    /// <summary>
    /// IDN-ATTR-002 AC5: an account of two organizations, one of which does not enable
    /// photos, shows none, to itself and to an administrator alike, and is answered as
    /// an account with no photo set; it shows the photo once both enable them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_002_AC5_AnAccountOfTwoOrganizationsShowsAPhotoOnlyWhereBothEnableThemAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        var showing = OrganizationId.New(_deployment.Clock);
        var hiding = OrganizationId.New(_deployment.Clock);

        _deployment.Memberships.Place(subject, showing);
        _deployment.Memberships.Place(subject, hiding);
        _deployment.Configuration.Set(Settings.PolicyDefault, Policies.SystemDefault with { Photos = true });
        _deployment.Configuration.Set(
            Settings.OrganizationPolicy,
            hiding.ToString(),
            PolicyOverride.None with { Photos = false });
        _deployment.Accounts.Shows(subject, Encoding.ASCII.GetBytes("a-photo"));
        _deployment.Gate.Grant(subject, Administration, Permissions.AccountManage);

        Answer own = await browser.SendAsync("GET", "/account/photo");
        Answer administered = await browser.SendAsync("GET", PathOf(subject, "photo"));

        _deployment.Configuration.Set(Settings.OrganizationPolicy, hiding.ToString(), PolicyOverride.None);

        Answer shown = await browser.SendAsync("GET", "/account/photo");

        foreach (Answer answer in new[] { own, administered })
        {
            Assert.Equal(StatusCodes.Status404NotFound, answer.Status);
            Assert.Equal(ErrorCodes.PhotoNotFound.ToString(), answer.Text("code"));
            Assert.Empty(answer.Json().GetProperty("details").EnumerateObject());
        }

        Assert.Equal(StatusCodes.Status200OK, shown.Status);
        Assert.Equal("a-photo", shown.Body);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the administrator committed after the gate
    /// step and before the first write refuses a suspension, a reactivation, the lifting
    /// of a restriction and the cancelling of a deletion, each leaving the account as it
    /// stood.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesEachTransitionAsync()
    {
        Session active = await MemberAsync();
        Session suspended = await MemberAsync();
        Session restricted = await MemberAsync();
        Session deleting = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.AccountManage);

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync("POST", PathOf(suspended.Subject, "suspend"))).Status);

        _deployment.Accounts.Stands(restricted.Subject, AccountState.Restricted);
        _deployment.Accounts.Deleting(deleting.Subject, DeletionOrigin.Self, _deployment.Clock.GetUtcNow().AddDays(-1));

        foreach ((SubjectId subject, string operation, AccountState stands) in new[]
        {
            (active.Subject, "suspend", AccountState.Active),
            (suspended.Subject, "reactivate", AccountState.Suspended),
            (restricted.Subject, "restriction/lift", AccountState.Restricted),
            (deleting.Subject, "delete/cancel", AccountState.Deleting),
        })
        {
            await RestrictedSinceTheGateStep.RefusesAsync(
                _deployment,
                () => browser.SendAsync("POST", PathOf(subject, operation)));

            Assert.Equal(stands, await StateAsync(subject));
        }

        Assert.Null(active.EndedAt);
    }

    /// <summary>
    /// REC-ADM-001 AC3 and chapter 09 section 8a (D-221): the contact read of a person
    /// record's subject answers <c>kind</c> <c>record</c>, its name, its contacts as held,
    /// its language, its state and a null time zone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ADM_001_AC3_TheContactReadOfARecordNamesItsKindAsync()
    {
        var shop = OrganizationId.New(_deployment.Clock);
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId clerk = _deployment.Directory.Created[^1].Subject;
        var subject = new SubjectId(Guid.CreateVersion7(_deployment.Clock.GetUtcNow()));

        _deployment.Organizations.Seed(shop);
        _deployment.Records.Hold(new HeldRecord(subject, shop, _deployment.Clock.GetUtcNow())
        {
            Language = "ar",
            Contacts =
            {
                [IdentifierKind.Phone] = new CollectedContact(IdentifierKind.Phone, "+441632960021", VerifiedAt: null),
            },
        });
        _deployment.Ties.Tie(subject, shop);
        _deployment.Gate.Grant(clerk, shop, Permissions.ContactRead);

        Answer contact = await browser.SendAsync("GET", PathOf(subject, "contact?organization=" + shop.Value));

        Assert.Equal(StatusCodes.Status200OK, contact.Status);
        Assert.Equal(
            ("record", "+441632960021", "ar", "active"),
            (contact.Text("kind"), contact.Text("phone"), contact.Text("language"), contact.Text("state")));
        Assert.Equal(
            JsonValueKind.Null,
            contact.Json().GetProperty("timeZone").ValueKind);
        Assert.Equal(JsonValueKind.Null, contact.Json().GetProperty("email").ValueKind);
    }

    /// <summary>
    /// IDN-READ-003 and chapter 09 section 8a (D-218): a holder of the reads in an
    /// organization reads the account of a person tied to it in the shape the row gives,
    /// finds it by its email address carried in the body, and reads its contact details.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_003_AnAccountIsReadFoundAndContactedAsync()
    {
        var shop = OrganizationId.New(_deployment.Clock);
        Session member = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId clerk = _deployment.Directory.Created[^1].Subject;

        _deployment.Organizations.Seed(shop);
        _deployment.Ties.Tie(member.Subject, shop);
        _deployment.Accounts.Registered(member.Subject, _deployment.Clock.GetUtcNow());
        _ = _deployment.Identifiers.Verified(member.Subject, IdentifierKind.Email, "ann@example.test");
        _deployment.Gate.Grant(clerk, shop, Permissions.AccountRead);
        _deployment.Gate.Grant(clerk, shop, Permissions.ContactRead);

        Answer read = await browser.SendAsync("GET", "/admin/accounts/" + member.Subject.Value + "?organization=" + shop.Value);
        Answer found = await browser.SendAsync(
            "POST",
            "/admin/accounts/find",
            ("identifier", "Ann@Example.test"),
            ("organization", shop.Value));
        Answer contact = await browser.SendAsync("GET", PathOf(member.Subject, "contact?organization=" + shop.Value));

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal(member.Subject.Value, read.Json().GetProperty("subject").GetGuid());
        Assert.Equal(
            "ann@example.test",
            read.Json().GetProperty("identifiers").GetProperty("emails")[0].GetProperty("value").GetString());
        Assert.Equal(StatusCodes.Status200OK, found.Status);
        Assert.Equal(member.Subject.Value, found.Json().GetProperty("subject").GetGuid());
        Assert.Equal(StatusCodes.Status200OK, contact.Status);
        Assert.Equal("ann@example.test", contact.Text("email"));
        Assert.Equal(("account", "active"), (contact.Text("kind"), contact.Text("state")));
    }

    /// <summary>
    /// IDN-READ-004 AC1 and chapter 09 section 8a (D-218): an account beyond the reach of
    /// the organization asked in is answered 404 in bytes identical to a subject no
    /// account bears, save the correlation identifier every answer carries; an organization
    /// that does not read is malformed, naming it, and one
    /// the deployment does not hold is not found.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_004_AC1_AnAccountBeyondTheReachIsAnsweredInIdenticalBytesAsync()
    {
        var shop = OrganizationId.New(_deployment.Clock);
        Session stranger = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Organizations.Seed(shop);
        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, shop, Permissions.ContactRead);

        Answer beyond = await browser.SendAsync("GET", PathOf(stranger.Subject, "contact?organization=" + shop.Value));
        Answer none = await browser.SendAsync(
            "GET", PathOf(SubjectId.New(_randomness), "contact?organization=" + shop.Value));
        Answer unread = await browser.SendAsync("GET", PathOf(stranger.Subject, "contact?organization=shop"));
        Answer unheld = await browser.SendAsync(
            "GET", PathOf(stranger.Subject, "contact?organization=" + OrganizationId.New(_deployment.Clock).Value));

        Assert.Equal(StatusCodes.Status404NotFound, beyond.Status);
        Assert.Equal(none.Status, beyond.Status);
        Assert.Equal(Without(none), Without(beyond));
        Assert.Equal(StatusCodes.Status400BadRequest, unread.Status);
        Assert.Equal("organization", unread.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal(StatusCodes.Status404NotFound, unheld.Status);
        Assert.Equal(ErrorCodes.OrganizationNotFound.ToString(), unheld.Text("code"));
    }

    /// <summary>
    /// IDN-READ-003 AC4 and chapter 09 section 8a (D-218): the standing read answers the
    /// account's state and each consent as the person's own read answers it, a withdrawn
    /// one with when it was taken back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_003_AC4_TheStandingIsReadAsThePersonsOwnReadAnswersItAsync()
    {
        var shop = OrganizationId.New(_deployment.Clock);
        Session member = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId clerk = _deployment.Directory.Created[^1].Subject;
        DateTimeOffset now = _deployment.Clock.GetUtcNow();

        _deployment.Reached.Hold(shop);
        _deployment.Reached.Tie(member.Subject, shop);
        _deployment.AccountStates.Hold(member.Subject, AccountState.Restricted);
        _deployment.Consents.Keep(
            member.Subject,
            new ConsentRecord(
                "recommendations", "privacy-notice", "1", ConsentMechanism.Dashboard, ConsentKind.Ordinary,
                now, now, null));
        _deployment.Gate.Grant(clerk, shop, Permissions.ConsentRead);

        Answer standing = await browser.SendAsync(
            "GET", PathOf(member.Subject, "standing?organization=" + shop.Value));
        Answer beyond = await browser.SendAsync(
            "GET", PathOf(SubjectId.New(_randomness), "standing?organization=" + shop.Value));

        Assert.Equal(StatusCodes.Status200OK, standing.Status);
        Assert.Equal(("account", "restricted"), (standing.Text("kind"), standing.Text("state")));
        Assert.Equal(
            "recommendations",
            standing.Json().GetProperty("consents")[0].GetProperty("purpose").GetString());
        Assert.NotEqual(
            System.Text.Json.JsonValueKind.Null,
            standing.Json().GetProperty("consents")[0].GetProperty("withdrawnAt").ValueKind);
        Assert.Equal(0, standing.Json().GetProperty("objections").GetArrayLength());
        Assert.Equal(StatusCodes.Status404NotFound, beyond.Status);
        Assert.Equal(ErrorCodes.AccountNotFound.ToString(), beyond.Text("code"));
    }

    /// <summary>
    /// IDN-READ-003 AC5 and chapter 09 section 8a (D-218): an administrator sets another
    /// person's administrator-only preference over the endpoint, answered 204 with no
    /// step-up, while setting it as one's own is refused
    /// <c>identity.preference.administratoronly</c> as before; a body without a reason is
    /// malformed, naming it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_READ_003_AC5_AnAdministratorSetsAnotherPersonsPreferencesAsync()
    {
        Session member = await MemberAsync();
        Browser browser = await Flow.SignedInAsync(_deployment);
        var badge = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["badge"] = "true",
        };

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.AccountManage);
        _deployment.Clock.Advance(Settings.SessionStepUpRecency.Default + TimeSpan.FromMinutes(1));

        Answer set = await browser.SendAsync(
            "PUT",
            PathOf(member.Subject, "preferences"),
            ("declared", badge),
            ("reason", "Ticket 4471"));
        Answer unreasoned = await browser.SendAsync(
            "PUT",
            PathOf(member.Subject, "preferences"),
            ("declared", badge));
        Answer own = await browser.SendAsync("PUT", "/account/preferences", ("declared", badge));

        Assert.Equal(StatusCodes.Status204NoContent, set.Status);
        Assert.Equal("true", (await _deployment.Accounts.PreferencesAsync(member.Subject, TestContext.Current.CancellationToken))
                .Values["badge"]);
        Assert.Equal(StatusCodes.Status400BadRequest, unreasoned.Status);
        Assert.Equal("reason", unreasoned.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, own.Status);
        Assert.Equal(ErrorCodes.PreferenceAdministratorOnly.ToString(), own.Text("code"));
    }

    private static string PathOf(SubjectId subject, string operation) =>
        "/admin/accounts/" + subject.Value + "/" + operation;

    // The body without the correlation identifier, which differs between any two
    // requests by design (API-CONV-002 AC2).
    private static string Without(Answer answer) =>
        answer.Body.Replace(answer.Text("correlationId"), string.Empty, StringComparison.Ordinal);

    private async Task<AccountState?> StateAsync(SubjectId subject) =>
        await _deployment.Accounts.StateAsync(subject, TestContext.Current.CancellationToken);

    private async Task<Session> MemberAsync()
    {
        var session = Session.Begin(
            SessionId.New(_deployment.Clock),
            SubjectId.New(_randomness),
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        _deployment.Accounts.Stands(session.Subject, AccountState.Active);

        await _deployment.Sessions.AddAsync(
            session,
            OpaqueToken.Draw(_randomness).Fingerprint(),
            OpaqueToken.Draw(_randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        return session;
    }
}
