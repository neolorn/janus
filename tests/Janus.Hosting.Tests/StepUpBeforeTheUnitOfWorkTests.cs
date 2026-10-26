using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Authorization.Grants;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// The operations whose step-up may ask the carrier's signal of a number, each judging
/// it before its unit of work begins: the signal is never asked inside an open
/// transaction, a <c>risk</c> answer's record is committed in a unit of work of its own
/// and stands after the step-up refuses, and every refusal the step-up must follow is
/// answered first, with no unit of work begun (CONV-DESIGN-002, CONV-DESIGN-003,
/// AUTH-FACT-002b, D-207).
/// </summary>
[Trait("kind", "unit")]
public sealed class StepUpBeforeTheUnitOfWorkTests : IAsyncLifetime
{
    private const string Domain = "example.test";

    private const string Listed = """{"domain":"example.test","reason":"Staff sign in with work addresses."}""";

    private const string Reasoned = """{"reason":"The record is published."}""";

    private const string Tightened = """{"requiredAssurance":"aal2","reason":"Staff hold administrative roles."}""";

    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly RoleName SystemAdministrator = RoleName.Parse("system-administrator");

    private static readonly GrantSubject Holder =
        GrantSubject.Of(new SubjectId(Guid.Parse("55555555-5555-4555-8555-555555555555")));

    private static readonly string[] Editing = ["article:read", "article:edit"];

    private readonly Deployment _deployment;

    // Whether the carrier was asked while a unit of work was open, which it never is.
    private bool _askedInside;

    /// <summary>
    /// A deployment administered by one organization and holding a second, whose
    /// carrier reports a recent change of SIM for every number, under a policy that
    /// lets a text code be presented at a step-up.
    /// </summary>
    public StepUpBeforeTheUnitOfWorkTests()
    {
        Deployment? deployment = null;

        _deployment = deployment = new Deployment(
            signals: new PhoneSignalProvider((_, _) =>
            {
                _askedInside |= deployment!.Work.Open;

                return ValueTask.FromResult(PhoneSignal.Risk);
            }));

        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
        _deployment.Organizations.Seed(Branch);
        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>([.. Policies.SystemDefault.LoginFactors, Factor.PhoneCode]),
            });
    }

    /// <summary>
    /// The seeded administrative role.
    /// </summary>
    /// <returns>The work of writing it.</returns>
    public async ValueTask InitializeAsync() =>
        await _deployment.Roles.CreateAsync(Role.Of(SystemAdministrator, Permissions.All), CancellationToken.None);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// CONV-DESIGN-003 AC13: a replacement of an organization's policy asks its step-up,
    /// and through it the signal, with no unit of work begun, and the record of the
    /// signal's <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_APolicyReplacementAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.OrganizationManage);

        Texted(subject);

        Refused(await administrator.SendAsync("PUT", PolicyOf(Branch), Tightened), subject);
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a domain added to an organization's lock asks its step-up,
    /// and through it the signal, with no unit of work begun, and the record of the
    /// signal's <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ADomainAddedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.DomainManage,
            Permissions.SystemAdminister);

        Texted(subject);

        Refused(await administrator.SendAsync("POST", DomainsOf(Branch), Listed), subject);
        Assert.Empty(await _deployment.Domains.OfAsync(Branch, CancellationToken.None));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a domain removed from an organization's lock asks its
    /// step-up, and through it the signal, with no unit of work begun, and the record of
    /// the signal's <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ADomainRemovedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.DomainManage,
            Permissions.SystemAdminister);

        Assert.Equal(StatusCodes.Status201Created, (await administrator.SendAsync("POST", DomainsOf(Branch), Listed)).Status);

        Texted(subject);

        Refused(await administrator.SendAsync("DELETE", DomainsOf(Branch) + "/" + Domain, Reasoned), subject);
        Assert.Contains(
            await _deployment.Domains.OfAsync(Branch, CancellationToken.None),
            domain => domain.IsListed);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a member added to a group asks its step-up, and through it
    /// the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AMemberAddedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        GroupId group = await GroupAsync(administrator, "Tellers");

        Texted(subject);

        Refused(await MemberAddedAsync(administrator, group, Holder), subject);
        Assert.Empty(await _deployment.Groups.MembersAsync(group, CancellationToken.None));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a member taken out of a group asks its step-up, and through
    /// it the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AMemberRemovedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        GroupId group = await GroupAsync(administrator, "Tellers");

        Assert.Equal(StatusCodes.Status204NoContent, (await MemberAddedAsync(administrator, group, Holder)).Status);

        Texted(subject);

        Refused(
            await administrator.SendAsync(
                "DELETE",
                "/admin/groups/" + group + "/members",
                ("subjectType", "user"),
                ("subjectId", Holder.Value),
                ("reason", "Moved on.")),
            subject);
        Assert.Equal([Holder], await _deployment.Groups.MembersAsync(group, CancellationToken.None));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a role defined asks its step-up, and through it the signal,
    /// with no unit of work begun, and the record of the signal's <c>risk</c> stands
    /// after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ARoleDefinedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.RoleManage);

        Texted(subject);

        Refused(await RoleDefinedAsync(administrator), subject);
        Assert.Null(await _deployment.Roles.FindAsync(RoleName.Parse("editor"), CancellationToken.None));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a role removed asks its step-up, and through it the signal,
    /// with no unit of work begun, and the record of the signal's <c>risk</c> stands
    /// after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ARoleRemovedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.RoleManage);

        Assert.Equal(StatusCodes.Status201Created, (await RoleDefinedAsync(administrator)).Status);

        Texted(subject);

        Refused(
            await administrator.SendAsync("DELETE", "/admin/roles/editor", ("reason", "No longer used.")),
            subject);
        Assert.NotNull(await _deployment.Roles.FindAsync(RoleName.Parse("editor"), CancellationToken.None));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a change of username asks its step-up, and through it the
    /// signal, with no unit of work begun, and the record of the signal's <c>risk</c>
    /// stands after the step-up refuses, the account holding no username.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AUsernameChangeAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        _deployment.Configuration.Set(Settings.IdentifiersUsernameEnabled, value: true);

        Browser person = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        Texted(subject);

        Refused(await person.SendAsync("PUT", "/account/profile", ("username", "kestrel")), subject);
        Assert.Equal(
            JsonValueKind.Null,
            (await person.SendAsync("GET", "/account")).Json().GetProperty("identifiers").GetProperty("username").ValueKind);
    }

    /// <summary>
    /// AUTH-SESS-005b AC4 (D-207): a replacement of the administrative organization's
    /// policy that would take its members below <c>aal2</c> is refused before any
    /// step-up is judged, with no unit of work begun and no signal asked, and changes
    /// nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_005b_AC4_AReplacementBelowTheFloorIsRefusedBeforeTheStepUpAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.OrganizationManage);

        Texted(subject);

        int rolledBack = _deployment.Work.RolledBack;
        Answer refused = await administrator.SendAsync(
            "PUT",
            PolicyOf(Administration),
            """{"requiredAssurance":"aal1","reason":"Back to the default."}""");

        Assert.Equal(ErrorCodes.ConfigurationValueBelowFloor.ToString(), refused.Text("code"));
        Unasked(rolledBack);
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// CONV-DESIGN-002 (D-207): a replacement judged no looser than the system policy
    /// before its step-up is judged again under the policy's lock, so one the system
    /// policy came to refuse after the first judgement is refused there, its unit of
    /// work rolled back and nothing changed (AUTH-STEP-002a).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_APolicyReplacementBelowTheSystemPolicyIsRefusedAgainUnderTheLockAsync()
    {
        (Browser administrator, _) = await AuthorisedAsync(Administration, Permissions.OrganizationManage);

        AtTheOperationsBegin(() => _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with { RequiredAssurance = AssuranceLevel.Aal2 }));

        int rolledBack = _deployment.Work.RolledBack;
        Answer refused = await administrator.SendAsync(
            "PUT",
            PolicyOf(Branch),
            """{"requiredAssurance":"aal1","reason":"Stated as the default."}""");

        Assert.Equal(ErrorCodes.ConfigurationPolicyBelowSystem.ToString(), refused.Text("code"));
        Assert.Equal(rolledBack + 1, _deployment.Work.RolledBack);
        Assert.False(_deployment.Work.Open);
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// CONV-DESIGN-002 (D-207): a removal of a domain that would turn the lock off is a
    /// loosening, and a caller who may not loosen the deployment is refused before the
    /// step-up is judged, with no unit of work begun and no signal asked (OPS-CFG-002).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_ADomainRemovalThatLoosensIsRefusedBeforeTheStepUpAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.DomainManage,
            Permissions.SystemAdminister);

        Assert.Equal(StatusCodes.Status201Created, (await administrator.SendAsync("POST", DomainsOf(Branch), Listed)).Status);

        _deployment.Gate.Revoke(subject, Administration, Permissions.SystemAdminister);
        Texted(subject);

        int rolledBack = _deployment.Work.RolledBack;
        Answer refused = await administrator.SendAsync("DELETE", DomainsOf(Branch) + "/" + Domain, Reasoned);

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
        Unasked(rolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-002 (D-207): a member that would close a cycle of groups is refused
    /// before the step-up is judged, with no unit of work begun and no signal asked
    /// (AUTHZ-GROUP-001).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AMemberClosingACycleIsRefusedBeforeTheStepUpAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Branch, Permissions.GroupManage);
        GroupId department = await GroupAsync(administrator, "Department");
        GroupId team = await GroupAsync(administrator, "Team");

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await MemberAddedAsync(administrator, department, GrantSubject.Of(team))).Status);

        Texted(subject);

        int rolledBack = _deployment.Work.RolledBack;
        Answer refused = await MemberAddedAsync(administrator, team, GrantSubject.Of(department));

        Assert.Equal(ErrorCodes.GroupCycle.ToString(), refused.Text("code"));
        Unasked(rolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-002 (D-207): a role a grant names is refused its removal before the
    /// step-up is judged, with no unit of work begun and no signal asked; and again
    /// under the role's lock where a grant came to name it after the first judgement
    /// (AUTHZ-GRANT-004).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_ARoleInUseIsRefusedBeforeTheStepUpAndAgainUnderTheLockAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.RoleManage);
        var editor = RoleName.Parse("editor");

        Assert.Equal(StatusCodes.Status201Created, (await RoleDefinedAsync(administrator)).Status);
        Assert.Equal(StatusCodes.Status201Created, (await RoleDefinedAsync(administrator, "writer")).Status);

        await GrantOfAsync(editor, subject);
        AuthenticatorId texted = Texted(subject);

        int rolledBack = _deployment.Work.RolledBack;
        Answer before = await administrator.SendAsync("DELETE", "/admin/roles/editor", ("reason", "No longer used."));

        Assert.Equal(ErrorCodes.RoleInUse.ToString(), before.Text("code"));
        Unasked(rolledBack);

        await _deployment.Authenticators.RemoveAsync(texted, CancellationToken.None);
        AtTheOperationsBegin(() => GrantOfAsync(RoleName.Parse("writer"), subject).GetAwaiter().GetResult());

        Answer under = await administrator.SendAsync("DELETE", "/admin/roles/writer", ("reason", "No longer used."));

        Assert.Equal(ErrorCodes.RoleInUse.ToString(), under.Text("code"));
        Assert.False(_deployment.Work.Open);
        Assert.NotNull(await _deployment.Roles.FindAsync(RoleName.Parse("writer"), CancellationToken.None));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13 (D-209): a host's bound action whose gate the session meets
    /// at the ask before the host's unit of work and no longer meets at the ask inside it,
    /// its proof having aged between them, is refused <c>auth.stepup.required</c> with the
    /// offer made where no signal is declared: the text code among the combinations, the
    /// carrier not asked and no consideration recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AHostsGateUnmetAtItsAskInsideItsUnitOfWorkAsksNoSignalAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        _ = await Flow.SignedInAsync(_deployment);

        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        Texted(subject);

        SessionId proved = await ProvedAsync(subject);

        await using AsyncServiceScope scope = _deployment.Scope();
        StepUpGuard guard = scope.ServiceProvider.GetRequiredService<StepUpGuard>();

        Assert.True(StepUpRefusal.Met(await HostsGateAsync(guard, subject, proved)));

        _deployment.Clock.Advance(Settings.SessionStepUpRecency.Default + TimeSpan.FromMinutes(1));
        _ = await _deployment.Work.BeginAsync(cancellationToken);

        StepUpChallenge inside = await HostsGateAsync(guard, subject, proved);

        await _deployment.Work.RollbackAsync();

        Error refused = StepUpRefusal.Of(inside);

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Equal("present", refused.Details["outcome"].GetString());
        Offered(refused.Details["options"]);
        Assert.False(_askedInside);
        Assert.Empty(_deployment.Considered.Records);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13 (D-209): a library operation with a step-up that a host calls
    /// inside its own unit of work, the end of another person's sessions, is refused
    /// <c>auth.stepup.required</c> with the offer made where no signal is declared, the
    /// carrier not asked and no consideration recorded; the signal is asked when the
    /// step-up is carried out, with no unit of work open and before any text is sent, and
    /// its <c>risk</c> withholds the text code there.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnOperationAHostCallsInsideItsUnitOfWorkAsksNoSignalAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.SessionRevokeAccount);
        SessionId session = _deployment.Sessions.All.Single(held => held.Subject == subject && held.EndedAt is null).Id;

        Texted(subject);
        _deployment.Authenticators.Hold(Generator(subject));

        Result outcome;

        await using (AsyncServiceScope scope = _deployment.Scope())
        {
            _ = await _deployment.Work.BeginAsync(cancellationToken);

            outcome = await scope.ServiceProvider.GetRequiredService<ISessions>()
                .RevokeAccountAsync(AccessContext.Of(subject), session, subject, cancellationToken);

            await _deployment.Work.RollbackAsync();
        }

        Error refused = outcome.Match<Error>(
            () => throw new InvalidOperationException("The operation was admitted."),
            error => error);

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Equal("present", refused.Details["outcome"].GetString());
        Offered(refused.Details["options"]);
        Assert.False(_askedInside);
        Assert.Empty(_deployment.Considered.Records);
        Assert.False(_deployment.Work.Open);

        string challenge = (await administrator.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address)))
            .Text("challengeId");
        int sent = _deployment.Sms.Taken.Count;

        Answer asked = await administrator.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", challenge),
            ("factor", "phoneCode"));

        Assert.Equal(StatusCodes.Status200OK, asked.Status);
        Assert.DoesNotContain(
            "phoneCode",
            asked.Json().GetProperty("required").EnumerateArray().Select(factor => factor.GetString()));
        Assert.Equal(sent, _deployment.Sms.Taken.Count);
        Assert.False(_askedInside);
        Assert.Equal(
            [(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)],
            _deployment.Considered.Records);
    }

    // The offer made where no signal is declared: the password and the text code among
    // the combinations, the text code not withheld.
    private static void Offered(JsonElement options) =>
        Assert.Contains(
            options.EnumerateArray(),
            combination => combination.EnumerateArray().Any(factor => factor.GetString() == "phoneCode"));

    // A time-based code generator beside the password, which reaches what the text code
    // reaches, so a step-up the signal withholds the text code from still offers it.
    private Authenticator Generator(SubjectId subject) =>
        Authenticator.Existing(
            AuthenticatorId.New(_deployment.Clock),
            subject,
            Factor.Totp,
            CredentialLabel.TryParse("Generator", out CredentialLabel label)
                ? label
                : throw new InvalidOperationException("The label does not read."),
            AuthenticatorState.Active,
            _deployment.Clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            new TotpMaterial(new byte[20], null),
            null,
            isPreferred: false,
            number: null);

    // A session of the person's own that reached two factors now, which meets every gate
    // of the system policy until its proof ages past the recency.
    private async Task<SessionId> ProvedAsync(SubjectId subject)
    {
        var session = Session.Begin(
            SessionId.New(_deployment.Clock),
            subject,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        await _deployment.Sessions.AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            TestContext.Current.CancellationToken);

        return session.Id;
    }

    // A gate a host binds its own action to, judged as the request's session is judged
    // at each ask of it.
    private static async Task<StepUpChallenge> HostsGateAsync(StepUpGuard guard, SubjectId subject, SessionId session) =>
        (await guard.ChallengeAsync(subject, session, "document:publish", TestContext.Current.CancellationToken))
        .Match(challenge => challenge, error => throw new InvalidOperationException(error.Code.ToString()));

    private static string PolicyOf(OrganizationId organization) => "/admin/organizations/" + organization + "/policy";

    private static string DomainsOf(OrganizationId organization) => "/admin/organizations/" + organization + "/domains";

    private static Task<Answer> MemberAddedAsync(Browser administrator, GroupId group, GrantSubject member) =>
        administrator.SendAsync(
            "POST",
            "/admin/groups/" + group + "/members",
            ("subjectType", member.Type is SubjectType.User ? "user" : "group"),
            ("subjectId", member.Value),
            ("reason", "Needs it."));

    private static Task<Answer> RoleDefinedAsync(Browser administrator, string name = "editor") =>
        administrator.SendAsync(
            "POST",
            "/admin/roles",
            ("name", name),
            ("permissions", Editing),
            ("reason", "Needs it."));

    private static async Task<GroupId> GroupAsync(Browser administrator, string name)
    {
        Answer created = await administrator.SendAsync(
            "POST",
            "/admin/groups",
            ("organization", Branch.Value),
            ("name", name),
            ("reason", "Needs it."));

        Assert.Equal(StatusCodes.Status201Created, created.Status);

        return new GroupId(Guid.Parse(created.Text("id")));
    }

    // The step-up refused, the signal asked with no unit of work open, and its record,
    // committed in a unit of work of its own, still standing once the operation is done.
    private void Refused(Answer answer, SubjectId subject)
    {
        Assert.Equal(StatusCodes.Status403Forbidden, answer.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), answer.Text("code"));
        Assert.False(_askedInside);
        Assert.False(_deployment.Work.Open);
        Assert.Equal(
            [(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)],
            _deployment.Considered.Records);
    }

    // What another transaction commits as the operation's unit of work opens, after the
    // judgement made without a lock: the request's session is read in a unit of work of
    // its own first, so the change is made at the second opening.
    private void AtTheOperationsBegin(Action change) =>
        _deployment.Work.Meanwhile = () => _deployment.Work.Meanwhile = change;

    // The refusal answered with no unit of work begun and nothing asked of the carrier:
    // every refusal made once the operation's unit of work began rolls it back.
    private void Unasked(int rolledBack)
    {
        Assert.Equal(rolledBack, _deployment.Work.RolledBack);
        Assert.False(_askedInside);
        Assert.Empty(_deployment.Considered.Records);
    }

    // The account holds a text code beside its password, so a gate its session has not
    // met costs what the two reach together and offers the text code, whose number the
    // carrier is asked about.
    private AuthenticatorId Texted(SubjectId subject)
    {
        var texted = AuthenticatorId.New(_deployment.Clock);

        _deployment.Authenticators.Hold(Authenticator.Existing(
            texted,
            subject,
            Factor.PhoneCode,
            CredentialLabel.TryParse("Number", out CredentialLabel label)
                ? label
                : throw new InvalidOperationException("The label does not read."),
            AuthenticatorState.Active,
            _deployment.Clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            null,
            null,
            isPreferred: false,
            number: Flow.Number));

        return texted;
    }

    // A grant of the role in the branch that stands, which names it.
    private async Task GrantOfAsync(RoleName role, SubjectId actor)
    {
        Grant grant = Grant
            .Create(
                GrantId.New(_deployment.Clock),
                GrantSubject.Of(actor),
                role,
                Branch,
                on: null,
                deny: false,
                GrantKind.Stored,
                expiresAt: null,
                actor,
                _deployment.Clock.GetUtcNow(),
                "Works in the branch.")
            .Match(created => created, error => throw new InvalidOperationException(error.Code.ToString()));

        await _deployment.AccessGrants.CreateAsync(grant, CancellationToken.None);
    }

    private async Task<(Browser Browser, SubjectId Subject)> AuthorisedAsync(
        OrganizationId organization,
        params Permission[] permissions)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        foreach (Permission permission in permissions)
        {
            _deployment.Gate.Grant(subject, organization, permission);
        }

        return (browser, subject);
    }
}
