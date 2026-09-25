using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Organizations;

/// <summary>
/// The domain lock over <c>/admin/organizations/{id}/domains</c> of chapter 09 section
/// 8a: a domain listed admits nothing until its TXT record is found, a verified domain
/// admits its addresses, a removed one refuses them and alerts, and a failed scheduled
/// check alerts and revokes nothing (REG-DOM-001, IDN-ORG-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationDomainEndpointTests : IAsyncDisposable
{
    private const string Domain = "example.test";

    private const string Listed = """{"domain":"example.test","reason":"Staff sign in with work addresses."}""";

    private const string Reasoned = """{"reason":"The record is published."}""";

    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private readonly Janus.Hosting.Tests.Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization and
    /// holding a second whose lock the tests change.
    /// </summary>
    public OrganizationDomainEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
        _deployment.Organizations.Seed(Branch);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REG-DOM-001 AC1: a listed domain admits no address until its record is found;
    /// the add answers the record to publish, and once it is found the domain's
    /// addresses sign in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_AC1_AListedDomainAdmitsNothingUntilVerifiedAsync()
    {
        (Browser administrator, SubjectId member) = await AuthorisedAsync();

        _deployment.Memberships.Place(member, Branch);

        Answer added = await administrator.SendAsync("POST", PathOf(Branch), Listed);
        JsonElement domain = added.Json();

        Assert.Equal(StatusCodes.Status201Created, added.Status);
        Assert.Equal(Domain, domain.GetProperty("domain").GetString());
        Assert.Equal("_identity-verify." + Domain, domain.GetProperty("recordName").GetString());
        Assert.StartsWith(
            "identity-domain-verification=",
            domain.GetProperty("recordValue").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, domain.GetProperty("verifiedAt").ValueKind);
        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed.ToString(), (await SignInAsync(Flow.Password)).Text("code"));

        _deployment.Dns.Publish(domain.GetProperty("recordName").GetString()!, domain.GetProperty("recordValue").GetString()!);

        Answer verified = await administrator.SendAsync("POST", PathOf(Branch) + "/" + Domain + "/verify", Reasoned);

        Assert.Equal(StatusCodes.Status200OK, verified.Status);
        Assert.NotEqual(JsonValueKind.Null, verified.Json().GetProperty("verifiedAt").ValueKind);
        Assert.Equal("deviceVerificationRequired", (await SignInAsync(Flow.Password)).Text("status"));
    }

    /// <summary>
    /// REG-DOM-001 and AUTH-ABUSE-003: the lock is told only once a factor has
    /// succeeded, so a wrong password against a locked address answers as any wrong
    /// password does.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_TheLockIsToldOnlyAfterAFactorSucceedsAsync()
    {
        (Browser administrator, SubjectId member) = await AuthorisedAsync();

        _deployment.Memberships.Place(member, Branch);
        _ = await administrator.SendAsync("POST", PathOf(Branch), Listed);

        Answer wrong = await SignInAsync("notthepassword");

        Assert.Equal(ErrorCodes.FactorRejected.ToString(), wrong.Text("code"));
    }

    /// <summary>
    /// REG-DOM-001 AC3: a scheduled check that no longer finds the record raises
    /// <c>domain-reverification-failed</c> and changes nothing else: the domain stays
    /// verified, the member keeps the session and the membership, and still signs in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_AC3_AFailedReverificationAlertsAndRevokesNothingAsync()
    {
        (Browser administrator, SubjectId member) = await AuthorisedAsync();

        _deployment.Memberships.Place(member, Branch);

        JsonElement listed = await VerifiedAsync(administrator);

        _deployment.Dns.Withdraw(listed.GetProperty("recordName").GetString()!);
        _deployment.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));

        Result<int> swept = await Sweep().SweepAsync(CancellationToken.None);
        LockedDomain checkedDomain = Assert.Single(_deployment.Domains.Held);

        Assert.Equal(1, swept.Match(count => count, _ => -1));
        Assert.Contains(
            _deployment.Events.Of<AlertRaised>(),
            alert => alert.Condition is AlertCondition.DomainReverificationFailed);
        Assert.False(checkedDomain.LastCheckPassed);
        Assert.True(checkedDomain.Admits);
        Assert.Equal(Branch, Assert.Single(await _deployment.Memberships.OfAsync(member, CancellationToken.None)));
        Assert.All(_deployment.Sessions.All, session => Assert.Null(session.EndedAt));
        Assert.Equal("deviceVerificationRequired", (await SignInAsync(Flow.Password)).Text("status"));
    }

    /// <summary>
    /// REG-DOM-001: a scheduled check that finds the record records it, and a domain
    /// checked within the interval is not looked at again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_AScheduledCheckRunsOncePerIntervalAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync();

        _ = await VerifiedAsync(administrator);

        Assert.Equal(0, (await Sweep().SweepAsync(CancellationToken.None)).Match(count => count, _ => -1));

        _deployment.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));

        Assert.Equal(1, (await Sweep().SweepAsync(CancellationToken.None)).Match(count => count, _ => -1));
        Assert.True(Assert.Single(_deployment.Domains.Held).LastCheckPassed);
        Assert.Empty(_deployment.Events.Of<AlertRaised>());
    }

    /// <summary>
    /// REG-DOM-001 AC4: once a domain is removed, sign-in with an address in it is
    /// refused, even where the removal left the lock empty, and the removal raises
    /// <c>domain-removed</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_AC4_ARemovedDomainRefusesSignInAndAlertsAsync()
    {
        (Browser administrator, SubjectId member) = await AuthorisedAsync();

        _deployment.Memberships.Place(member, Branch);
        _ = await VerifiedAsync(administrator);

        Answer removed = await administrator.SendAsync("DELETE", PathOf(Branch) + "/" + Domain, Reasoned);
        AlertRaised alert = Assert.Single(
            _deployment.Events.Of<AlertRaised>(),
            raised => raised.Condition is AlertCondition.DomainRemoved);

        Assert.Equal(StatusCodes.Status204NoContent, removed.Status);
        Assert.Equal(Domain, alert.Details["domain"].GetString());
        Assert.Null((await OverrideAsync(Branch)).EmailDomains);
        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed.ToString(), (await SignInAsync(Flow.Password)).Text("code"));
    }

    /// <summary>
    /// REG-DOM-001 and AUTH-ABUSE-003: a sign-in link asked for a locked address is
    /// answered as any other ask and sends nothing, and one sent before the domain was
    /// removed no longer signs in, though the sign-in was opened with the number.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_AC4_ALinkNoLongerSignsInToARemovedDomainAsync()
    {
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);
        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>(
                    [.. Janus.Core.Policies.SystemDefault.LoginFactors, Factor.EmailLink]),
            });

        foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
        {
            _deployment.Templates.Set(
                MessageKind.SignInLink,
                kind,
                "en",
                new MessageTemplate(kind is SendKind.Email ? "link" : null, "{code} {token}"));
        }

        (Browser administrator, SubjectId member) = await AuthorisedAsync();

        _deployment.Memberships.Place(member, Branch);
        _ = await VerifiedAsync(administrator);

        var asking = new Browser(_deployment);
        string challenge = await BegunAsync(asking, Flow.Number);

        _ = await asking.SendAsync("POST", "/auth/link", ("identifier", Flow.Address));

        string token = Flow.Token(_deployment, IdentifierKind.Email);
        _ = await administrator.SendAsync("DELETE", PathOf(Branch) + "/" + Domain, Reasoned);
        int sent = _deployment.Mail.Taken.Count;

        Answer pressed = await asking.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "emailLink"),
            ("linkToken", token),
            ("press", true));
        var other = new Browser(_deployment);

        _ = await other.SendAsync("GET", "/auth/session");

        // The link just sent holds the address's restriction for its interval, and a
        // link the lock withholds counts as one sent would (AUTH-ABUSE-002 AC3).
        _deployment.Clock.Advance(TimeSpan.FromMinutes(1));

        Answer asked = await other.SendAsync("POST", "/auth/link", ("identifier", Flow.Address));

        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed.ToString(), pressed.Text("code"));
        Assert.Equal(StatusCodes.Status202Accepted, asked.Status);
        Assert.Equal(sent, _deployment.Mail.Taken.Count);
    }

    /// <summary>
    /// IDN-ORG-006 AC1: with <c>emailDomains</c> off no address is refused on grounds of
    /// its domain.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_006_AC1_WithTheLockOffNoAddressIsRefusedAsync()
    {
        (_, SubjectId member) = await AuthorisedAsync();

        _deployment.Memberships.Place(member, Branch);

        Assert.Equal("deviceVerificationRequired", (await SignInAsync(Flow.Password)).Text("status"));
    }

    /// <summary>
    /// IDN-ORG-006 AC2: with the lock on, a member's sign-in email outside the verified
    /// list is refused with <c>identity.identifier.domainnotallowed</c>; the lock is the
    /// organization's, so an account that is not its member is not held to it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_006_AC2_AnAddressOutsideTheVerifiedListIsRefusedAsync()
    {
        (Browser administrator, SubjectId member) = await AuthorisedAsync();

        JsonElement added = (await administrator.SendAsync(
            "POST",
            PathOf(Branch),
            """{"domain":"corp.example","reason":"Staff sign in with work addresses."}""")).Json();

        _deployment.Dns.Publish(added.GetProperty("recordName").GetString()!, added.GetProperty("recordValue").GetString()!);
        _ = await administrator.SendAsync("POST", PathOf(Branch) + "/corp.example/verify", Reasoned);

        Assert.Equal("deviceVerificationRequired", (await SignInAsync(Flow.Password)).Text("status"));

        _deployment.Memberships.Place(member, Branch);

        Answer refused = await SignInAsync(Flow.Password);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed.ToString(), refused.Text("code"));
    }

    /// <summary>
    /// IDN-ORG-006 AC3: the lock is a policy value of the organization, inherited from
    /// the system policy until the organization lists a domain, and changed at runtime
    /// through the one operation that writes configuration; the system policy itself
    /// locks no domain.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_006_AC3_TheLockIsAPolicyValueChangedWithoutADeployAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync();

        _deployment.Gate.Grant(subject, Administration, Permissions.OrganizationManage);
        _deployment.Gate.Grant(subject, Administration, Permissions.ConfigurationManage);

        JsonElement before = (await administrator.SendAsync("GET", PolicyOf(Branch))).Json().GetProperty("emailDomains");

        _ = await administrator.SendAsync("POST", PathOf(Branch), Listed);

        JsonElement after = (await administrator.SendAsync("GET", PolicyOf(Branch))).Json().GetProperty("emailDomains");
        Janus.Authentication.Configuration.ConfigurationChange change = Assert.Single(_deployment.Changes.Written);
        Answer system = await administrator.SendAsync(
            "PUT",
            "/admin/config/policy.default",
            """{"value":{"requiredAssurance":"aal1","loginFactors":["passkey","password"],"gates":{},"credentialRedundancy":"advisory","selfServiceRecovery":true,"emailDomains":["example.test"]},"reason":"Lock everyone."}""");

        Assert.Equal(0, before.GetProperty("value").GetArrayLength());
        Assert.False(before.GetProperty("overridden").GetBoolean());
        Assert.Equal([Domain], after.GetProperty("value").EnumerateArray().Select(listed => listed.GetString()));
        Assert.True(after.GetProperty("overridden").GetBoolean());
        Assert.Equal(Settings.OrganizationPolicy.For(Branch.ToString()), change.Key);
        Assert.True(change.Loosening);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, system.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed.ToString(), system.Text("code"));
        Assert.Equal("emailDomains", system.Json().GetProperty("details").GetProperty("field").GetString());
    }

    /// <summary>
    /// OPS-CFG-002 and 09 section 8a: listing a domain is a loosening, so it needs
    /// <c>system:administer</c> beside <c>domain:manage</c>, is the <c>domain:manage</c>
    /// step-up action, and is written down with the domain and the reason.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_002_ListingADomainIsASteppedUpLooseningAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Gate.Grant(subject, Administration, Permissions.DomainManage);

        Answer withheld = await browser.SendAsync("POST", PathOf(Branch), Listed);

        _deployment.Gate.Grant(subject, Administration, Permissions.SystemAdminister);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer stale = await browser.SendAsync("POST", PathOf(Branch), Listed);

        Assert.Equal(StatusCodes.Status403Forbidden, withheld.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), withheld.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, stale.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), stale.Text("code"));
        Assert.Empty(_deployment.Domains.Held);
        Assert.Empty(_deployment.OrganizationChanges.Changes);
    }

    /// <summary>
    /// 09 section 8a: every change is written down with the domain and the reason, and
    /// the list answers each domain with where its verification stands.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_EveryChangeIsWrittenDownAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync();

        _ = await VerifiedAsync(administrator);

        JsonElement listed = Assert.Single((await administrator.SendAsync("GET", PathOf(Branch))).Json().EnumerateArray());

        _ = await administrator.SendAsync("DELETE", PathOf(Branch) + "/" + Domain, Reasoned);

        Assert.Equal(
            [AuditActions.OrganizationDomainAdded, AuditActions.OrganizationDomainVerified, AuditActions.OrganizationDomainRemoved],
            _deployment.OrganizationChanges.Changes.Select(change => change.Action));
        Assert.All(_deployment.OrganizationChanges.Changes, change => Assert.Equal(Domain, change.Domain));
        Assert.Equal(Domain, listed.GetProperty("domain").GetString());
        Assert.True(listed.GetProperty("lastCheckPassed").GetBoolean());
        Assert.Empty((await administrator.SendAsync("GET", PathOf(Branch))).Json().EnumerateArray());
    }

    /// <summary>
    /// REG-DOM-001 and D-153: a domain whose record does not carry its token, or whose
    /// record could not be looked up, is refused with <c>identity.domain.unverified</c>
    /// and nothing is written; a domain listed anew draws a token it never had.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_AnUnprovedDomainIsNotVerifiedAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync();

        JsonElement first = (await administrator.SendAsync("POST", PathOf(Branch), Listed)).Json();

        _deployment.Dns.Publish(first.GetProperty("recordName").GetString()!, "identity-domain-verification=someoneelse");

        Answer wrong = await administrator.SendAsync("POST", PathOf(Branch) + "/" + Domain + "/verify", Reasoned);

        _deployment.Dns.Publish(first.GetProperty("recordName").GetString()!, first.GetProperty("recordValue").GetString()!);
        _deployment.Dns.Unreachable = true;

        Answer unreachable = await administrator.SendAsync("POST", PathOf(Branch) + "/" + Domain + "/verify", Reasoned);

        _ = await administrator.SendAsync("DELETE", PathOf(Branch) + "/" + Domain, Reasoned);

        JsonElement second = (await administrator.SendAsync("POST", PathOf(Branch), Listed)).Json();

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, wrong.Status);
        Assert.Equal(ErrorCodes.DomainUnverified.ToString(), wrong.Text("code"));
        Assert.Equal(ErrorCodes.DomainUnverified.ToString(), unreachable.Text("code"));
        Assert.DoesNotContain(
            _deployment.OrganizationChanges.Changes,
            change => change.Action == AuditActions.OrganizationDomainVerified);
        Assert.NotEqual(
            first.GetProperty("recordValue").GetString(),
            second.GetProperty("recordValue").GetString());
    }

    /// <summary>
    /// API-CONV-002 and IDN-ORG-006: a domain is read in its canonical ASCII form, so
    /// the Unicode and the ASCII forms of one domain are one; what is not a domain of
    /// two labels, a missing reason, an unlisted domain and an organization the
    /// deployment does not hold are refused naming the member; listing a domain twice
    /// changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_WhatAChangeNamesMustBeReadableAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync();

        Answer unicode = await administrator.SendAsync(
            "POST",
            PathOf(Branch),
            """{"domain":"Bücher.Example","reason":"Staff sign in with work addresses."}""");
        Answer ascii = await administrator.SendAsync(
            "POST",
            PathOf(Branch),
            """{"domain":"xn--bcher-kva.example","reason":"Staff sign in with work addresses."}""");
        Answer single = await administrator.SendAsync("POST", PathOf(Branch), """{"domain":"localhost","reason":"Why not."}""");
        Answer spaced = await administrator.SendAsync("POST", PathOf(Branch), """{"domain":"not a domain.test","reason":"Why not."}""");
        Answer unreasoned = await administrator.SendAsync("POST", PathOf(Branch), """{"domain":"example.test"}""");
        Answer unlisted = await administrator.SendAsync("POST", PathOf(Branch) + "/other.test/verify", Reasoned);
        Answer unheld = await administrator.SendAsync("POST", PathOf(new OrganizationId(Guid.NewGuid())), Listed);
        Answer absent = await administrator.SendAsync("DELETE", PathOf(Branch) + "/other.test", Reasoned);

        Assert.Equal(StatusCodes.Status201Created, unicode.Status);
        Assert.Equal("xn--bcher-kva.example", unicode.Text("domain"));
        Assert.Equal(unicode.Text("recordValue"), ascii.Text("recordValue"));
        Assert.Single(_deployment.Domains.Held);
        Assert.Equal("domain", Member(single));
        Assert.Equal("domain", Member(spaced));
        Assert.Equal("reason", Member(unreasoned));
        Assert.Equal("domain", Member(unlisted));
        Assert.Equal("id", Member(unheld));
        Assert.Equal(StatusCodes.Status204NoContent, absent.Status);
    }

    /// <summary>
    /// 09 section 8a: the lock is governed from the administrative organization; the
    /// permission held in the organization itself is not enough to read it or to change
    /// it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_TheLockIsGovernedFromTheAdministrativeOrganizationAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Gate.Grant(subject, Branch, Permissions.DomainManage);
        _deployment.Gate.Grant(subject, Branch, Permissions.SystemAdminister);

        Answer read = await browser.SendAsync("GET", PathOf(Branch));
        Answer added = await browser.SendAsync("POST", PathOf(Branch), Listed);

        Assert.Equal(StatusCodes.Status403Forbidden, read.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), read.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, added.Status);
        Assert.Empty(_deployment.Domains.Held);
    }

    /// <summary>
    /// CONV-CODE-006 AC2: a body missing a member adding, verifying or removing a domain
    /// requires is refused naming the member before the service is reached, so a caller
    /// the service would refuse for want of the permission is answered for the body,
    /// and the lock holds nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_006_AC2_ABodyMissingAMemberIsRefusedBeforeTheServiceAsync()
    {
        Browser caller = await Flow.SignedInAsync(_deployment);

        Answer unnamed = await caller.SendAsync("POST", PathOf(Branch), ("reason", "Locking the branch."));
        Answer unreasoned = await caller.SendAsync("POST", PathOf(Branch), ("domain", Domain), ("reason", null));
        Answer unverified = await caller.SendAsync("POST", PathOf(Branch) + "/" + Domain + "/verify", "{}");
        Answer unremoved = await caller.SendAsync("DELETE", PathOf(Branch) + "/" + Domain, "{}");

        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), unnamed.Text("code"));
        Assert.Equal("domain", Member(unnamed));
        Assert.Equal("reason", Member(unreasoned));
        Assert.Equal("reason", Member(unverified));
        Assert.Equal("reason", Member(unremoved));
        Assert.Empty(_deployment.Domains.Held);
    }

    private static string PathOf(OrganizationId organization) => "/admin/organizations/" + organization + "/domains";

    private static string PolicyOf(OrganizationId organization) => "/admin/organizations/" + organization + "/policy";

    private static string Member(Answer answer)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);

        return answer.Json().GetProperty("details").GetProperty("member").GetString()!;
    }

    // A browser that has a first contact and a sign-in open against an identifier the
    // registration flow registered.
    private static async Task<string> BegunAsync(Browser browser, string identifier = Flow.Address)
    {
        _ = await browser.SendAsync("GET", "/auth/session");

        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", identifier));

        Assert.Equal(StatusCodes.Status200OK, began.Status);

        return began.Text("challengeId");
    }

    private DomainReverification Sweep() =>
        new(
            _deployment.Domains,
            _deployment.Dns,
            _deployment.Configuration,
            _deployment.Events,
            _deployment.Work,
            _deployment.Clock);

    // A sign-in with the registered address, from a browser of its own.
    private async Task<Answer> SignInAsync(string password)
    {
        var browser = new Browser(_deployment);
        string challenge = await BegunAsync(browser);

        return await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", password));
    }

    // Lists the domain and verifies it by the record the add answered.
    private async Task<JsonElement> VerifiedAsync(Browser administrator)
    {
        JsonElement added = (await administrator.SendAsync("POST", PathOf(Branch), Listed)).Json();

        _deployment.Dns.Publish(added.GetProperty("recordName").GetString()!, added.GetProperty("recordValue").GetString()!);

        Answer verified = await administrator.SendAsync("POST", PathOf(Branch) + "/" + Domain + "/verify", Reasoned);

        Assert.Equal(StatusCodes.Status200OK, verified.Status);

        return verified.Json();
    }

    // The override the deployment holds for the organization.
    private async Task<PolicyOverride> OverrideAsync(OrganizationId organization) =>
        (await _deployment.Configuration.ReadAsync(
            Settings.OrganizationPolicy,
            organization.ToString(),
            CancellationToken.None))
            .Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private async Task<(Browser Browser, SubjectId Subject)> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Gate.Grant(subject, Administration, Permissions.DomainManage);
        _deployment.Gate.Grant(subject, Administration, Permissions.SystemAdminister);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        return (browser, subject);
    }
}
