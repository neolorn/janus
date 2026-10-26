using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Factors;
using Janus.Authorization.Grants;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Tests.Credentials;
using Janus.Privacy.Outbox;
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
    private const string AppPasswords = "/account/mail/apppasswords/";

    private const string Domain = "example.test";

    private const string Invited = """{"email":"invited@elsewhere.test"}""";

    private const string ProviderSubject = "110169484474386276334";

    private const string RelyingParty = "identity.example.test";

    private const string Listed = """{"domain":"example.test","reason":"Staff sign in with work addresses."}""";

    private const string Reasoned = """{"reason":"The record is published."}""";

    private const string Tightened = """{"requiredAssurance":"aal2","reason":"Staff hold administrative roles."}""";

    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly RoleName Reader = RoleName.Parse("reader");

    private static readonly RoleName SystemAdministrator = RoleName.Parse("system-administrator");

    private static readonly GrantSubject Holder =
        GrantSubject.Of(new SubjectId(Guid.Parse("55555555-5555-4555-8555-555555555555")));

    private static readonly string[] Editing = ["article:read", "article:edit"];

    private readonly Deployment _deployment;

    // Whether the carrier was asked while a unit of work was open, which it never is.
    private bool _askedInside;

    // What the carrier answers, a recent change unless a test says otherwise.
    private PhoneSignal _answered = PhoneSignal.Risk;

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

                return ValueTask.FromResult(_answered);
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
    /// CONV-DESIGN-003 AC13 (D-209): the guard, judging a gate a host binds its own action
    /// to while a unit of work is open, on a session whose proof met the gate before the
    /// unit of work began and has aged past it since, refuses <c>auth.stepup.required</c>
    /// with the offer made where no signal is declared: the text code among the
    /// combinations, the carrier not asked and no consideration recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_TheGuardJudgingAHostsGateInsideAUnitOfWorkAsksNoSignalAsync()
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

    /// <summary>
    /// CONV-DESIGN-003 AC13: the account's own deactivation asks its step-up, and through
    /// it the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ADeactivationAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        Refused(await person.SendAsync("POST", "/account/deactivate"), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the account's own deletion asks its step-up, and through it
    /// the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnAccountDeletionAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        Refused(await person.SendAsync("POST", "/account/delete"), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a password set on a session whose proof has aged asks its
    /// step-up, and through it the signal, with no unit of work begun, and the record of
    /// the signal's <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_APasswordSetAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        Aged();

        Refused(await person.SendAsync("POST", "/account/password", ("password", "lemonbutterscotch")), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the enrolment of a code generator asks its step-up, and
    /// through it the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AGeneratorEnrolmentAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        Refused(await person.SendAsync("POST", "/account/factors/totp/begin", ("label", "This phone")), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the beginning of a text code's enrolment asks its step-up,
    /// and through it the signal, with no unit of work begun, and the record of the
    /// signal's <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ATextCodeEnrolmentBegunAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        Refused(
            await person.SendAsync(
                "POST",
                "/account/factors/phonecode/begin",
                ("identifier", await NumberOfAsync(person)),
                ("label", "This phone")),
            subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the confirmation of a text code's enrolment, begun while the
    /// session met the gate, asks its step-up again, and through it the signal, with no
    /// unit of work begun, and the record of the signal's <c>risk</c> stands after the
    /// step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ATextCodeEnrolmentConfirmedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        Browser person = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        // The enrolment asks the carrier about the number it texts, which must stand
        // clear for the code to be sent.
        _answered = PhoneSignal.Clear;

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await person.SendAsync(
                "POST",
                "/account/factors/phonecode/begin",
                ("identifier", await NumberOfAsync(person)),
                ("label", "This phone"))).Status);

        _answered = PhoneSignal.Risk;
        _deployment.Considered.Records.Clear();
        _deployment.Authenticators.Hold(Generator(subject));
        Texted(subject);

        Refused(await person.SendAsync("POST", "/account/factors/phonecode/confirm", ("code", "123456")), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the generation of recovery codes asks its step-up, and
    /// through it the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_RecoveryCodesGeneratedAskTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        Refused(await person.SendAsync("POST", "/account/recoverycodes"), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the removal of a credential asks its step-up, and through it
    /// the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses, the credential still held.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ACredentialRemovedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();
        Authenticator generator = Generator(subject);

        _deployment.Authenticators.Hold(generator);

        Refused(await person.SendAsync("DELETE", "/account/credentials/" + generator.Id), subject);
        Assert.NotNull(await _deployment.Authenticators.FindAsync(generator.Id, CancellationToken.None));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13, AUTH-STEP-007: the upgrade of a security key to a passkey
    /// costs the phishing resistance both the passkey and the key the account holds
    /// reach, which no text code gives, so its step-up offers none and asks the carrier
    /// nothing, whatever the account's text code.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AKeyUpgradeOffersNoTextCodeAndAsksNoSignalAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();
        var key = Authenticator.Existing(
            AuthenticatorId.New(_deployment.Clock),
            subject,
            Factor.SecurityKey,
            CredentialLabel.TryParse("Key", out CredentialLabel label)
                ? label
                : throw new InvalidOperationException("The label does not read."),
            AuthenticatorState.Active,
            _deployment.Clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            null,
            new WebAuthnMaterial(
                RandomNumberGenerator.GetBytes(16),
                SoftwareAuthenticator.Holding().PublicKey,
                SoftwareAuthenticator.Es256,
                RelyingParty,
                null,
                false,
                false,
                ["usb"]),
            isPreferred: false);

        _deployment.Authenticators.Hold(key);

        Answer refused = await person.SendAsync("POST", "/account/credentials/" + key.Id + "/upgrade");

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), refused.Text("code"));
        Assert.DoesNotContain(
            refused.Json().GetProperty("details").GetProperty("options").EnumerateArray(),
            combination => combination.EnumerateArray().Any(factor => factor.GetString() == "phoneCode"));
        Assert.Empty(_deployment.Considered.Records);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the beginning of a passkey's registration asks its step-up,
    /// and through it the signal, with no unit of work begun, and the record of the
    /// signal's <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AKeyRegistrationBegunAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        Refused(await person.SendAsync("POST", "/auth/webauthn/register/begin", ("kind", "passkey")), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the completion of a passkey's registration, begun while the
    /// session met the gate, asks its step-up again, and through it the signal, with no
    /// unit of work begun, and the record of the signal's <c>risk</c> stands after the
    /// step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AKeyRegistrationCompletedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        Browser person = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        Answer begun = await person.SendAsync("POST", "/auth/webauthn/register/begin", ("kind", "passkey"));

        Assert.Equal(StatusCodes.Status200OK, begun.Status);

        Texted(subject);

        string created = new JsonObject
        {
            ["credential"] = SoftwareAuthenticator.Written(SoftwareAuthenticator.Holding().Attested(
                begun.Text("challenge"),
                "https://" + RelyingParty,
                RelyingParty,
                (byte)(SoftwareAuthenticator.UserPresent | SoftwareAuthenticator.UserVerified),
                transports: ["internal"])),
            ["label"] = "This phone",
        }.ToJsonString();

        Refused(await person.SendAsync("POST", "/auth/webauthn/register/complete", created), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: an identifier added asks its step-up, and through it the
    /// signal, with no unit of work begun, and the record of the signal's <c>risk</c>
    /// stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnIdentifierAddedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        Refused(
            await person.SendAsync("POST", "/account/identifiers", ("kind", "email"), ("value", "second@example.test")),
            subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: an identifier replaced asks its step-up, and through it the
    /// signal, with no unit of work begun, and the record of the signal's <c>risk</c>
    /// stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnIdentifierReplacedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();
        string address = await IdentifierOfAsync(person, "emails");

        Refused(
            await person.SendAsync("PUT", "/account/identifiers/" + address + "/replace", ("value", "replaced@example.test")),
            subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: an identifier removed asks its step-up, and through it the
    /// signal, with no unit of work begun, and the record of the signal's <c>risk</c>
    /// stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnIdentifierRemovedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();
        Guid going = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, "second@example.test").Value;

        Refused(await person.SendAsync("DELETE", "/account/identifiers/" + going), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a provider's link asked for asks its step-up, and through it
    /// the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <param name="provider">The provider linked.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("google")]
    [InlineData("apple")]
    public async Task CONV_DESIGN_003_AC13_ALinkAskedForAsksTheSignalWithNoUnitOfWorkBegunAsync(string provider)
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        Refused(await person.SendAsync("POST", "/account/link/" + provider), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a start to link a provider asks the link's step-up before the
    /// browser leaves, and through it the signal, with no unit of work begun, and the
    /// record of the signal's <c>risk</c> stands after the browser is sent back.
    /// </summary>
    /// <param name="provider">The provider linked.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("google")]
    [InlineData("apple")]
    public async Task CONV_DESIGN_003_AC13_AStartToLinkAsksTheSignalWithNoUnitOfWorkBegunAsync(string provider)
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        ReturnedRefused(await person.SendAsync("GET", LinkStarted(provider)), subject);
        Assert.Equal(0, _deployment.ProviderAttempts.Count);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the return of a link started while the session met the gate
    /// asks the link's step-up again, and through it the signal, with no unit of work
    /// begun, and the record of the signal's <c>risk</c> stands after the browser is sent
    /// back, nothing linked.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AReturnToLinkAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        Browser person = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        string authorization = (await person.SendAsync("GET", LinkStarted("google"))).Location
            ?? throw new InvalidOperationException("The start forwarded nowhere.");

        Texted(subject);

        string code = _deployment.SocialProviders.Issue(Factor.Google, authorization, new ProviderPerson(ProviderSubject));
        Answer forwarded = await person.SendAsync(
            "POST",
            "/callbacks/providers/google/return",
            "code=" + Uri.EscapeDataString(code) + "&state="
                + Uri.EscapeDataString(SocialProvidersInMemory.Parameter(authorization, "state")!),
            header: false,
            origin: null,
            token: false,
            contentType: "application/x-www-form-urlencoded");

        ReturnedRefused(
            await person.SendAsync(
                "GET",
                forwarded.Location ?? throw new InvalidOperationException("The return forwarded nowhere.")),
            subject);
        Assert.Null(await _deployment.Authenticators.ByProviderAsync(Factor.Google, ProviderSubject, CancellationToken.None));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a provider unlinked asks its step-up, and through it the
    /// signal, with no unit of work begun, and the record of the signal's <c>risk</c>
    /// stands after the step-up refuses.
    /// </summary>
    /// <param name="provider">The provider unlinked.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("google")]
    [InlineData("apple")]
    public async Task CONV_DESIGN_003_AC13_AnUnlinkAsksTheSignalWithNoUnitOfWorkBegunAsync(string provider)
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        await _deployment.Authenticators.LinkAsync(
            Authenticator.Linked(
                AuthenticatorId.New(_deployment.Clock),
                subject,
                provider is "google" ? Factor.Google : Factor.Apple,
                CredentialLabel.Of(new DeviceDescription("Firefox", "Fedora")),
                _deployment.Clock.GetUtcNow()),
            ProviderSubject,
            CancellationToken.None);

        Refused(await person.SendAsync("DELETE", "/account/link/" + provider), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: an app password created asks its step-up, and through it the
    /// signal, with no unit of work begun, and the record of the signal's <c>risk</c>
    /// stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnAppPasswordCreatedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await MailboxHolderAsync();

        Texted(subject);

        Refused(await person.SendAsync("POST", AppPasswords, ("label", "Phone")), subject);
        Assert.Empty(_deployment.MailServer.Secrets);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: an app password revoked asks its step-up, and through it the
    /// signal, with no unit of work begun, and the record of the signal's <c>risk</c>
    /// stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnAppPasswordRevokedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await MailboxHolderAsync();
        Answer created = await person.SendAsync("POST", AppPasswords, ("label", "Phone"));

        Assert.Equal(StatusCodes.Status200OK, created.Status);

        Texted(subject);

        Refused(await person.SendAsync("DELETE", AppPasswords + created.Text("id")), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: an export of the person's data asks its step-up, and through
    /// it the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnExportAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser person, SubjectId subject) = await TextingPersonAsync();

        Refused(await person.SendAsync("GET", "/privacy/export?format=human"), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the end of every session asks its step-up, and through it
    /// the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses, the caller's session still open.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_EverySessionEndedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.SessionRevoke);

        Texted(subject);

        Refused(await administrator.SendAsync("POST", "/admin/sessions/revoke-all"), subject);
        Assert.Equal(StatusCodes.Status200OK, (await administrator.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the end of another account's sessions asks its step-up, and
    /// through it the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses, the account's session still open.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnAccountsSessionsEndedAskTheSignalWithNoUnitOfWorkBegunAsync()
    {
        Session member = await MemberAsync(AccountState.Active);
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.SessionRevokeAccount);

        Texted(subject);

        Refused(await administrator.SendAsync("POST", AccountPath(member.Subject, "sessions/revoke")), subject);
        Assert.Null(member.EndedAt);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: each change an administrator makes to another account's
    /// standing asks its step-up, and through it the signal, with no unit of work begun,
    /// and the record of the signal's <c>risk</c> stands after the step-up refuses, the
    /// account left as it stood: a suspension, a reactivation, a restriction lifted and
    /// a deletion cancelled.
    /// </summary>
    /// <param name="operation">The route's last segments.</param>
    /// <param name="standing">How the account stands before it.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("suspend", AccountState.Active)]
    [InlineData("reactivate", AccountState.Suspended)]
    [InlineData("restriction/lift", AccountState.Restricted)]
    [InlineData("delete/cancel", AccountState.Deleting)]
    public async Task CONV_DESIGN_003_AC13_AnAccountsStandingChangedAsksTheSignalWithNoUnitOfWorkBegunAsync(
        string operation,
        AccountState standing)
    {
        Session member = await MemberAsync(standing);
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.AccountManage);

        Texted(subject);

        Refused(await administrator.SendAsync("POST", AccountPath(member.Subject, operation)), subject);
        Assert.Equal(standing, await StandingAsync(member.Subject));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a takedown asks its step-up, and through it the signal, with
    /// no unit of work begun, and the record of the signal's <c>risk</c> stands after the
    /// step-up refuses, the account still active.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ATakedownAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        var member = new SubjectId(Guid.NewGuid());

        _deployment.AccountStates.Hold(member, AccountState.Active);

        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.TakedownExecute);

        Texted(subject);

        Refused(await TakenDownAsync(administrator, member), subject);
        Assert.Equal(AccountState.Active, _deployment.AccountStates.Of(member));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the reversal of a takedown asks its step-up, and through it
    /// the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses, the account still being deleted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ATakedownReversedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        var member = new SubjectId(Guid.NewGuid());

        _deployment.AccountStates.Hold(member, AccountState.Active);

        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.TakedownExecute);

        Assert.Equal(StatusCodes.Status202Accepted, (await TakenDownAsync(administrator, member)).Status);

        Texted(subject);

        Refused(
            await administrator.SendAsync(
                "POST",
                "/admin/accounts/" + member.Value + "/takedown/reverse",
                ("reason", "an adult, misjudged")),
            subject);
        Assert.Equal(AccountState.Deleting, _deployment.AccountStates.Of(member));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the generation of a break-glass credential asks its step-up,
    /// and through it the signal, with no unit of work begun, and the record of the
    /// signal's <c>risk</c> stands after the step-up refuses, nothing generated.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ABreakGlassGenerationAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.SystemAdminister);

        Texted(subject);

        Refused(await administrator.SendAsync("POST", "/admin/break-glass/generate"), subject);
        Assert.Empty(_deployment.BreakGlass.Issues);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: each change of configuration with a step-up asks it, and
    /// through it the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses, nothing written: a key loosened and
    /// the alert destinations replaced.
    /// </summary>
    /// <param name="key">The key changed.</param>
    /// <param name="value">The value it is changed to, as JSON.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("session.aal2.inactivity", "\"PT2H\"")]
    [InlineData("alerting.email.destinations", "[\"elsewhere@example.test\"]")]
    public async Task CONV_DESIGN_003_AC13_AConfigurationChangeAsksTheSignalWithNoUnitOfWorkBegunAsync(
        string key,
        string value)
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        Texted(subject);

        Refused(await ConfiguredAsync(administrator, key, value), subject);
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a category's retention shortened, which loosens it, asks its
    /// step-up, and through it the signal, with no unit of work begun, and the record of
    /// the signal's <c>risk</c> stands after the step-up refuses, the period lengthened
    /// before it the one in force.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ARetentionShortenedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.ConfigurationManage,
            Permissions.SystemAdminister);

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await ConfiguredAsync(administrator, "retention.statement", "\"P2000D\"")).Status);

        Texted(subject);

        Refused(await ConfiguredAsync(administrator, "retention.statement", "\"P1900D\""), subject);
        Assert.Single(_deployment.Changes.Written);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the completion of an erasure that failed asks its step-up,
    /// and through it the signal, with no unit of work begun, and the record of the
    /// signal's <c>risk</c> stands after the step-up refuses, the delivery still failed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnErasureCompletedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        DateTimeOffset attempted = _deployment.Clock.GetUtcNow().AddHours(-2);
        var delivery = Delivery.Of(new SubjectId(Guid.NewGuid()), SubjectEventKind.TakedownExecuted, attempted);

        delivery.Attempted(attempted, TimeSpan.FromSeconds(30), 2.0m, 1.0);
        delivery.Fail();
        await _deployment.Outbox.AddAsync(delivery, CancellationToken.None);

        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.PrivacyRequestManage);

        Texted(subject);

        Refused(await administrator.SendAsync("POST", "/admin/erasures/" + delivery.Id.Value + "/complete"), subject);
        Assert.Equal(ErasureStatus.Failed, _deployment.Outbox.Held(delivery.Id).Status);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the fulfilment of a privacy request asks its step-up, and
    /// through it the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_APrivacyRequestFulfilledAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        _deployment.Documents.Hold(
            new DocumentVersion("privacy-notice", "1", "en", "The notice.", [], _deployment.Clock.GetUtcNow()));
        _deployment.Configuration.Set(Settings.PrivacyCalendarTimeZone, "Africa/Cairo");

        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.PrivacyRequestManage);

        _deployment.AccountStates.Hold(subject, AccountState.Active);

        Answer submitted = await administrator.SendAsync(
            "POST",
            "/privacy/requests",
            ("type", "restriction"),
            ("detail", "the recorded address is disputed"));

        Texted(subject);

        Refused(
            await administrator.SendAsync(
                "POST",
                "/admin/privacy/requests/" + submitted.Json().GetProperty("requestId").GetGuid() + "/fulfil"),
            subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a grant asks its step-up, and through it the signal, with no
    /// unit of work begun, and the record of the signal's <c>risk</c> stands after the
    /// step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AGrantAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        await _deployment.Roles.CreateAsync(Role.Of(Reader, [Permission.Parse("article:read")]), CancellationToken.None);

        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Branch, Permissions.GrantManage);

        Texted(subject);

        Refused(await GrantedAsync(administrator), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the revocation of a grant asks its step-up, and through it
    /// the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AGrantRevokedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        await _deployment.Roles.CreateAsync(Role.Of(Reader, [Permission.Parse("article:read")]), CancellationToken.None);

        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Branch, Permissions.GrantManage);
        Answer granted = await GrantedAsync(administrator);

        Assert.Equal(StatusCodes.Status201Created, granted.Status);

        Texted(subject);

        Refused(
            await administrator.SendAsync("DELETE", "/admin/grants/" + granted.Text("id"), ("reason", "No longer needed.")),
            subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the deletion of an organization asks its step-up, and
    /// through it the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnOrganizationDeletedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.OrganizationManage);

        Texted(subject);

        Refused(await administrator.SendAsync("POST", OrganizationOf(Branch) + "/delete", Reasoned), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the cancellation of an organization's deletion asks its
    /// step-up, and through it the signal, with no unit of work begun, and the record of
    /// the signal's <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnOrganizationsDeletionCancelledAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.OrganizationManage);

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await administrator.SendAsync("POST", OrganizationOf(Branch) + "/delete", Reasoned)).Status);

        Texted(subject);

        Refused(await administrator.SendAsync("POST", OrganizationOf(Branch) + "/delete/cancel", Reasoned), subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: a domain of an organization's lock verified asks its
    /// step-up, and through it the signal, with no unit of work begun, and the record of
    /// the signal's <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ADomainVerifiedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.DomainManage,
            Permissions.SystemAdminister);

        Assert.Equal(StatusCodes.Status201Created, (await administrator.SendAsync("POST", DomainsOf(Branch), Listed)).Status);

        Texted(subject);

        Refused(
            await administrator.SendAsync("POST", DomainsOf(Branch) + "/" + Domain + "/verify", Reasoned),
            subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: an invitation issued asks its step-up, and through it the
    /// signal, with no unit of work begun, and the record of the signal's <c>risk</c>
    /// stands after the step-up refuses, nothing issued.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnInvitationIssuedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Branch, Permissions.MembershipManage);

        Texted(subject);

        Refused(await administrator.SendAsync("POST", OrganizationOf(Branch) + "/invitations", Invited), subject);
        Assert.Empty(_deployment.Invitations.Held);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: an invitation naming a role, whose issue's own step-up the
    /// session meets, asks the step-up a grant is, and through it the signal, with no
    /// unit of work begun, and the record of the signal's <c>risk</c> stands after the
    /// step-up refuses, nothing issued.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AnInvitationNamingARoleAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        Gated(StepUpAction.InvitationIssue, new Gate(GateLevel.Aal1, PhishingResistant: false, TimeSpan.FromMinutes(15)));
        _ = _deployment.RoleCatalogue.Define(Reader.ToString(), Permission.Parse("article:read"));

        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Branch,
            Permissions.MembershipManage,
            Permissions.GrantManage);

        Texted(subject);

        Refused(
            await administrator.SendAsync(
                "POST",
                OrganizationOf(Branch) + "/invitations",
                """{"email":"invited@elsewhere.test","roles":["reader"]}"""),
            subject);
        Assert.Empty(_deployment.Invitations.Held);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the end of a membership asks its step-up, and through it the
    /// signal, with no unit of work begun, and the record of the signal's <c>risk</c>
    /// stands after the step-up refuses, nothing ended.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AMembershipEndedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        var member = new SubjectId(Guid.NewGuid());

        _deployment.Memberships.Place(member, Branch);

        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Branch, Permissions.MembershipManage);

        Texted(subject);

        Refused(await administrator.SendAsync("DELETE", OrganizationOf(Branch) + "/memberships/" + member), subject);
        Assert.Empty(_deployment.Endings.Ended);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: the approval of another person's recovery asks its step-up,
    /// and through it the signal, with no unit of work begun, and the record of the
    /// signal's <c>risk</c> stands after the step-up refuses.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_ARecoveryApprovedAsksTheSignalWithNoUnitOfWorkBegunAsync()
    {
        Session member = await MemberAsync(AccountState.Active);

        _ = _deployment.Identifiers.Verified(member.Subject, IdentifierKind.Email, "member@example.test");

        (Browser administrator, SubjectId subject) = await AuthorisedAsync(Administration, Permissions.RecoveryApprove);

        Texted(subject);

        Refused(
            await administrator.SendAsync(
                "POST",
                "/admin/recovery/approve",
                ("subject", member.Subject.ToString()),
                ("reason", "The member lost every factor."),
                ("channelUsed", "member@example.test")),
            subject);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC13: each change to the restriction set asks its step-up, and
    /// through it the signal, with no unit of work begun, and the record of the signal's
    /// <c>risk</c> stands after the step-up refuses: a restriction edited, one deleted
    /// and credit granted against one.
    /// </summary>
    /// <param name="method">The request's method.</param>
    /// <param name="path">The route.</param>
    /// <param name="body">The request's body.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(
        "PUT",
        "/admin/restrictions/sms.destination",
        """{"key":"destination","buckets":[{"max":10,"interval":"PT24H","window":"sliding"}],"reason":"a busier week"}""")]
    [InlineData("DELETE", "/admin/restrictions/sms.source", """{"reason":"a load test"}""")]
    [InlineData(
        "POST",
        "/admin/restrictions/sms.destination/grant",
        """{"keyValue":"+201001234567","credit":3,"reason":"their carrier dropped both codes"}""")]
    public async Task CONV_DESIGN_003_AC13_ARestrictionChangedAsksTheSignalWithNoUnitOfWorkBegunAsync(
        string method,
        string path,
        string body)
    {
        (Browser administrator, SubjectId subject) = await AuthorisedAsync(
            Administration,
            Permissions.RestrictionEdit,
            Permissions.RestrictionGrant,
            Permissions.SystemAdminister);

        Texted(subject);

        Refused(await administrator.SendAsync(method, path, body), subject);
    }

    // A person signed in on a password session whose account also holds a text code.
    private async Task<(Browser Person, SubjectId Subject)> TextingPersonAsync()
    {
        Browser person = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        Texted(subject);

        return (person, subject);
    }

    // The id of the first of the account's identifiers of a kind, as the account reads.
    private static async Task<string> IdentifierOfAsync(Browser person, string kind) =>
        (await person.SendAsync("GET", "/account")).Json()
            .GetProperty("identifiers")
            .GetProperty(kind)[0]
            .GetProperty("id")
            .GetString()!;

    private static Task<string> NumberOfAsync(Browser person) => IdentifierOfAsync(person, "phones");

    // The system policy that lets a text code be presented, with one action's gate set
    // as given.
    private void Gated(StepUpAction action, Gate gate)
    {
        var gates = Policies.SystemDefault.Gates.ToDictionary();

        gates[action] = gate;

        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>([.. Policies.SystemDefault.LoginFactors, Factor.PhoneCode]),
                Gates = gates,
            });
    }

    // The session's proof aged past the recency every gate asks of it.
    private void Aged() =>
        _deployment.Clock.Advance(Settings.SessionStepUpRecency.Default + TimeSpan.FromMinutes(1));

    // Another person's account, standing as given, with a session open.
    private async Task<Session> MemberAsync(AccountState standing)
    {
        var session = Session.Begin(
            SessionId.New(_deployment.Clock),
            new SubjectId(Guid.NewGuid()),
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        if (standing is AccountState.Deleting)
        {
            _deployment.Accounts.Deleting(session.Subject, DeletionOrigin.Self, _deployment.Clock.GetUtcNow().AddDays(-1));
        }
        else if (standing is AccountState.Suspended)
        {
            _deployment.Accounts.Suspended(session.Subject, SuspensionOrigin.Administrator);
        }
        else
        {
            _deployment.Accounts.Stands(session.Subject, standing);
        }

        await _deployment.Sessions.AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            TestContext.Current.CancellationToken);

        return session;
    }

    private async Task<AccountState?> StandingAsync(SubjectId subject) =>
        await _deployment.Accounts.StateAsync(subject, CancellationToken.None);

    // INT-MAIL-006: a person holding the mailbox a membership of the administrative
    // organization gave them, whose registry holds the mail server's client.
    private async Task<(Browser Person, SubjectId Subject)> MailboxHolderAsync()
    {
        await _deployment.Clients.AddAsync(
            new OidcClient(
                "mail-server",
                "mail-server",
                OidcClientKind.Protocol,
                "https://mail.example.test/callback",
                ["openid", "email", "offline_access"]),
            Encoding.UTF8.GetBytes("the-mail-servers-secret"),
            _deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

        Browser person = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        var mailbox = Mailbox.Reserved(
            EmailAddress.TryParse("person@staff.example.test", out EmailAddress address)
                ? address
                : throw new InvalidOperationException("The address does not read."),
            _deployment.Clock.GetUtcNow());

        mailbox.Hold(subject);
        _deployment.Mailboxes.Held.Add(mailbox);

        return (person, subject);
    }

    private static Task<Answer> ConfiguredAsync(Browser administrator, string key, string value) =>
        administrator.SendAsync(
            "PUT",
            "/admin/config/" + key,
            "{\"value\":" + value + ",\"reason\":\"A change of course.\"}");

    private static Task<Answer> TakenDownAsync(Browser administrator, SubjectId member) =>
        administrator.SendAsync(
            "POST",
            "/admin/accounts/" + member.Value + "/takedown",
            ("trigger", "staff-report"),
            ("reason", "seen at the counter with a school card"));

    private static Task<Answer> GrantedAsync(Browser administrator) =>
        administrator.SendAsync(
            "POST",
            "/admin/grants",
            ("subjectType", "user"),
            ("subjectId", Holder.Value),
            ("resourceType", "organization"),
            ("resourceId", Branch.ToString()),
            ("role", Reader.ToString()),
            ("reason", "Needs it."));

    private static string LinkStarted(string provider) =>
        "/auth/providers/" + provider + "?intent=link&returnTo=" + Uri.EscapeDataString("/account/profile");

    private static string AccountPath(SubjectId subject, string operation) =>
        "/admin/accounts/" + subject.Value + "/" + operation;

    private static string OrganizationOf(OrganizationId organization) => "/admin/organizations/" + organization;

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

    // A gate a host binds its own action to, judged by the guard against the session, as
    // the request's session is judged at the first ask of it.
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
        AskedOutside(subject);
    }

    // As a refusal, for a route that answers by sending the browser back to where it
    // came from with the code.
    private void ReturnedRefused(Answer answer, SubjectId subject)
    {
        Assert.Equal(StatusCodes.Status303SeeOther, answer.Status);
        Assert.EndsWith("?error=" + ErrorCodes.StepUpRequired, answer.Location, StringComparison.Ordinal);
        AskedOutside(subject);
    }

    private void AskedOutside(SubjectId subject)
    {
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
