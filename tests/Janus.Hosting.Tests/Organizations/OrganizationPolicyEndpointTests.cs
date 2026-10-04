using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Organizations;

/// <summary>
/// An organization's policy over <c>/admin/organizations/{id}/policy</c> of chapter 09
/// section 8a: read resolved with each field marked, and replaced by an override no
/// looser than the system policy, stepped up, reasoned and written down
/// (AUTH-STEP-002a, D-143, OPS-CFG-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationPolicyEndpointTests : IAsyncDisposable
{
    private const string Tightened = """{"requiredAssurance":"aal2","reason":"Staff hold administrative roles."}""";

    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private readonly Janus.Hosting.Tests.Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization and
    /// holding a second.
    /// </summary>
    public OrganizationPolicyEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
        _deployment.Organizations.Seed(Branch);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// D-143: the policy is read resolved, each field and each gate marked with whether
    /// its value is the organization's own or inherited from the system policy.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002a_ThePolicyIsReadWithEachFieldMarkedAsync()
    {
        Browser administrator = await AuthorisedAsync();
        var gate = new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5));

        _deployment.Configuration.Set(
            Settings.OrganizationPolicy,
            Branch.ToString(),
            PolicyOverride.None with
            {
                RequiredAssurance = AssuranceLevel.Aal2,
                Gates = new Dictionary<StepUpAction, Gate> { [StepUpAction.PolicyChange] = gate },
            });

        Answer read = await administrator.SendAsync("GET", PathOf(Branch));
        JsonElement policy = read.Json();
        JsonElement changed = policy.GetProperty("gates").GetProperty("policy:change");
        JsonElement deleted = policy.GetProperty("gates").GetProperty("organization:delete");

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("aal2", policy.GetProperty("requiredAssurance").GetProperty("value").GetString());
        Assert.True(policy.GetProperty("requiredAssurance").GetProperty("overridden").GetBoolean());
        Assert.False(policy.GetProperty("loginFactors").GetProperty("overridden").GetBoolean());
        Assert.Equal("aal2", changed.GetProperty("value").GetProperty("level").GetString());
        Assert.Equal("PT5M", changed.GetProperty("value").GetProperty("maxAge").GetString());
        Assert.True(changed.GetProperty("overridden").GetBoolean());
        Assert.Equal("reachable", deleted.GetProperty("value").GetProperty("level").GetString());
        Assert.False(deleted.GetProperty("overridden").GetBoolean());
        Assert.False(policy.GetProperty("credentialRedundancy").GetProperty("overridden").GetBoolean());
        Assert.False(policy.GetProperty("selfServiceRecovery").GetProperty("overridden").GetBoolean());
        Assert.Equal(0, policy.GetProperty("emailDomains").GetProperty("value").GetArrayLength());
        Assert.False(policy.GetProperty("emailDomains").GetProperty("overridden").GetBoolean());
        Assert.False(policy.GetProperty("photos").GetProperty("value").GetBoolean());
        Assert.False(policy.GetProperty("photos").GetProperty("overridden").GetBoolean());
    }

    /// <summary>
    /// AUTH-STEP-002a and D-143: a field the system policy has since raised past the
    /// organization's own value is inherited, since the system's is what is in force.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002a_AFieldTheSystemHasOvertakenIsInheritedAsync()
    {
        Browser administrator = await AuthorisedAsync();

        _deployment.Configuration.Set(
            Settings.OrganizationPolicy,
            Branch.ToString(),
            PolicyOverride.None with { LoginFactors = new HashSet<Factor> { Factor.Passkey, Factor.Password } });
        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with { LoginFactors = new HashSet<Factor> { Factor.Passkey } });

        JsonElement factors = (await administrator.SendAsync("GET", PathOf(Branch))).Json().GetProperty("loginFactors");

        Assert.Equal(["passkey"], factors.GetProperty("value").EnumerateArray().Select(factor => factor.GetString()));
        Assert.False(factors.GetProperty("overridden").GetBoolean());
    }

    /// <summary>
    /// AUTH-STEP-002a and OPS-CFG-005: a tightening replaces the override, is written
    /// down with who and why as every runtime write is, and records what it raised for
    /// the organization's members (AUTH-FACT-017).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002a_ATighteningIsWrittenDownAsync()
    {
        Browser administrator = await AuthorisedAsync();

        Answer replaced = await administrator.SendAsync("PUT", PathOf(Branch), Tightened);
        Janus.Authentication.Configuration.ConfigurationChange change = Assert.Single(_deployment.Changes.Written);
        IReadOnlyList<PolicyRaise> raised = await _deployment.Raises.OfAsync(Branch, CancellationToken.None);

        Assert.Equal(StatusCodes.Status204NoContent, replaced.Status);
        Assert.Equal(AssuranceLevel.Aal2, (await OverrideAsync(Branch)).RequiredAssurance);
        Assert.Equal(Settings.OrganizationPolicy.For(Branch.ToString()), change.Key);
        Assert.False(change.Loosening);
        Assert.Equal("Staff hold administrative roles.", change.Reason);
        Assert.Equal(PolicyField.RequiredAssurance, Assert.Single(raised).Field);
        Assert.Empty(await _deployment.Raises.OfAsync(null, CancellationToken.None));
    }

    /// <summary>
    /// AUTH-STEP-002a: a field stated looser than the system policy is refused, naming
    /// the field, and nothing is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002a_AFieldLooserThanTheSystemIsRefusedAsync()
    {
        Browser administrator = await AuthorisedAsync();

        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with
            {
                RequiredAssurance = AssuranceLevel.Aal2,
                SelfServiceRecovery = false,
            });

        Answer floor = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"requiredAssurance":"aal1","reason":"Fewer prompts."}""");
        Answer factors = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"loginFactors":["passkey","emailCode"],"reason":"Fewer prompts."}""");
        Answer gate = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"gates":{"policy:change":{"level":"aal1","phishingResistant":false,"maxAge":"PT15M"}},"reason":"Fewer prompts."}""");
        Answer recovery = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"selfServiceRecovery":true,"reason":"Fewer prompts."}""");

        Assert.Equal("requiredAssurance", Below(floor));
        Assert.Equal("loginFactors", Below(factors));
        Assert.Equal("gates", Below(gate));
        Assert.Equal("selfServiceRecovery", Below(recovery));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// OPS-CFG-002 AC6 and X3 of D-166 (178): a policy change is decided on the values
    /// in force under the locks of the system policy's row and the organization's,
    /// taken in that order inside its one unit of work, which the write joins; a
    /// refusal made under them rolls that unit of work back before it answers
    /// (CONV-DESIGN-003).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC6_APolicyChangeIsDecidedUnderItsRowsLocksAsync()
    {
        Browser administrator = await AuthorisedAsync();
        ConfigurationKey member = Settings.OrganizationPolicy.For(Branch.ToString());

        _deployment.Configuration.Held.Clear();

        Answer replaced = await administrator.SendAsync("PUT", PathOf(Branch), Tightened);

        Assert.Equal(StatusCodes.Status204NoContent, replaced.Status);
        Assert.Equal([Settings.PolicyDefault.Key, member, member], _deployment.Configuration.Held);

        _deployment.Configuration.Held.Clear();
        _deployment.Work.Reset();
        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with { RequiredAssurance = AssuranceLevel.Aal2 });

        Answer refused = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"requiredAssurance":"aal1","reason":"Fewer prompts."}""");

        Assert.Equal("requiredAssurance", Below(refused));
        Assert.Equal([Settings.PolicyDefault.Key, member], _deployment.Configuration.Held);
        Assert.False(_deployment.Work.Open);
        Assert.Equal(1, _deployment.Work.RolledBack);
        Assert.Equal(_deployment.Work.Opened, _deployment.Work.Committed + _deployment.Work.RolledBack);
    }

    /// <summary>
    /// OPS-CFG-002: giving up an override the organization held is a loosening, which
    /// also needs <c>system:administer</c> and is written down as one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_CFG_002_ALooseningAlsoNeedsTheSystemPermissionAsync()
    {
        (Browser administrator, SubjectId subject) = await SignedInAsync();

        _deployment.Gate.Grant(subject, Administration, Permissions.OrganizationManage);
        _deployment.Configuration.Set(
            Settings.OrganizationPolicy,
            Branch.ToString(),
            PolicyOverride.None with { RequiredAssurance = AssuranceLevel.Aal2 });

        Answer withheld = await administrator.SendAsync("PUT", PathOf(Branch), """{"reason":"Back to the default."}""");

        Assert.Equal(StatusCodes.Status403Forbidden, withheld.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), withheld.Text("code"));
        Assert.Equal(AssuranceLevel.Aal2, (await OverrideAsync(Branch)).RequiredAssurance);

        _deployment.Gate.Grant(subject, Administration, Permissions.SystemAdminister);

        Answer loosened = await administrator.SendAsync("PUT", PathOf(Branch), """{"reason":"Back to the default."}""");

        Assert.Equal(StatusCodes.Status204NoContent, loosened.Status);
        Assert.Null((await OverrideAsync(Branch)).RequiredAssurance);
        Assert.True(Assert.Single(_deployment.Changes.Written).Loosening);
    }

    /// <summary>
    /// OPS-ALERT-001 AC1, D-083: giving up a gate the organization asked more of raises
    /// the High alert under the organization, naming its policy key and the gate.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AC1_AnOrganizationPolicyAskingLessAtAGateRaisesTheAlertAsync()
    {
        (Browser administrator, SubjectId subject) = await SignedInAsync();

        _deployment.Gate.Grant(subject, Administration, Permissions.OrganizationManage);
        _deployment.Gate.Grant(subject, Administration, Permissions.SystemAdminister);
        _deployment.Configuration.Set(
            Settings.OrganizationPolicy,
            Branch.ToString(),
            PolicyOverride.None with
            {
                Gates = new Dictionary<StepUpAction, Gate>
                {
                    [StepUpAction.PolicyChange] = new(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5)),
                },
            });

        Answer loosened = await administrator.SendAsync("PUT", PathOf(Branch), """{"reason":"Back to the default."}""");
        AlertRaised raised = Assert.Single(
            _deployment.Events.Of<AlertRaised>(),
            alert => alert.Condition is AlertCondition.StepUpPolicyWeakened);

        Assert.Equal(StatusCodes.Status204NoContent, loosened.Status);
        Assert.Equal(AlertSeverity.High, raised.Severity);
        Assert.Equal(Alerts.Key(AlertCondition.StepUpPolicyWeakened, scope: null, Branch.ToString()), Alerts.Deduplication(raised.IdempotencyKey));
        Assert.Equal("policy." + Branch, raised.Details["key"].GetString());
        Assert.Equal(["policy:change"], raised.Details["gates"].EnumerateArray().Select(gate => gate.GetString()));
    }

    /// <summary>
    /// AUTH-STEP-001 and 09 section 8a: a change of policy is the <c>policy:change</c>
    /// step-up action, so a session whose proof is no longer recent changes nothing;
    /// reading the policy is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_001_AChangeOfPolicyIsAStepUpActionAsync()
    {
        Browser administrator = await AuthorisedAsync();

        _deployment.Clock.Advance(TimeSpan.FromMinutes(16));

        Answer replaced = await administrator.SendAsync("PUT", PathOf(Branch), Tightened);
        Answer read = await administrator.SendAsync("GET", PathOf(Branch));

        Assert.Equal(StatusCodes.Status403Forbidden, replaced.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), replaced.Text("code"));
        Assert.Null((await OverrideAsync(Branch)).RequiredAssurance);
        Assert.Empty(_deployment.Changes.Written);
        Assert.Equal(StatusCodes.Status200OK, read.Status);
    }

    /// <summary>
    /// AUTH-SESS-005b: the administrative organization's floor of AAL2 is stated, so a
    /// replacement of its policy that does not state it is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_005b_TheAdministrativeOrganizationKeepsItsFloorAsync()
    {
        Browser administrator = await AuthorisedAsync();

        Answer unstated = await administrator.SendAsync(
            "PUT",
            PathOf(Administration),
            """{"selfServiceRecovery":false,"reason":"No self-service for staff."}""");
        Answer stated = await administrator.SendAsync(
            "PUT",
            PathOf(Administration),
            """{"requiredAssurance":"aal2","selfServiceRecovery":false,"reason":"No self-service for staff."}""");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unstated.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueBelowFloor.ToString(), unstated.Text("code"));
        Assert.Equal("requiredAssurance", unstated.Json().GetProperty("details").GetProperty("field").GetString());
        Assert.Equal(StatusCodes.Status204NoContent, stated.Status);
        Assert.Equal(AssuranceLevel.Aal2, (await OverrideAsync(Administration)).RequiredAssurance);
    }

    /// <summary>
    /// Chapter 10 section 4.1a, API-CONV-002 and 09 section 8a: a replacement names only
    /// the fields of the policy object, never the domain lock, with values the object
    /// takes and a reason; anything else is refused, naming the member where it is the
    /// request that is malformed, and a reason absent as a configuration change without
    /// one is, naming the organization's policy key. A member the object does not have is
    /// refused before any permission is asked.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002a_WhatAReplacementNamesMustBeReadableAsync()
    {
        (Browser administrator, SubjectId subject) = await SignedInAsync();

        Answer withheld = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"emailDomains":["example.com"],"reason":"Staff only."}""");

        _deployment.Gate.Grant(subject, Administration, Permissions.OrganizationManage);

        Answer misspelt = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"requiredAssurence":"aal2","reason":"Staff hold administrative roles."}""");
        Answer locked = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"emailDomains":["example.com"],"reason":"Staff only."}""");
        Answer unreasoned = await administrator.SendAsync("PUT", PathOf(Branch), """{"requiredAssurance":"aal2"}""");
        Answer blank = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"requiredAssurance":"aal2","reason":"  "}""");
        Answer overlong = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"requiredAssurance":"aal2","reason":""" + "\"" + new string('r', 1025) + "\"}");
        Answer numbered = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"requiredAssurance":"aal2","reason":7}""");
        Answer listed = await administrator.SendAsync("PUT", PathOf(Branch), "[]");
        Answer unreadable = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"requiredAssurance":"aal9","reason":"Staff hold administrative roles."}""");

        Assert.Equal("requiredAssurence", Member(misspelt));
        Assert.Equal("emailDomains", Member(withheld));
        Assert.Equal("emailDomains", Member(locked));
        Assert.Equal(PolicyKey(Branch), Unreasoned(unreasoned));
        Assert.Equal(PolicyKey(Branch), Unreasoned(blank));
        Assert.Equal("reason", Member(overlong));
        Assert.Equal("reason", Member(numbered));
        Assert.Equal(StatusCodes.Status400BadRequest, listed.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), listed.Text("code"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unreadable.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed.ToString(), unreadable.Text("code"));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// 09 section 8a: a replacement asked in process without a reason is refused as a
    /// configuration change without one is, naming the organization's policy key, and
    /// nothing is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002a_AReplacementInProcessWithoutAReasonIsRefusedAsync()
    {
        (_, SubjectId subject) = await SignedInAsync();

        _deployment.Gate.Grant(subject, Administration, Permissions.OrganizationManage);

        await using AsyncServiceScope scope = _deployment.Scope();
        IOrganizations organizations = scope.ServiceProvider.GetRequiredService<IOrganizations>();

        Result replaced = await organizations.ReplacePolicyAsync(
            AccessContext.Of(subject),
            SessionId.New(_deployment.Clock),
            Branch,
            PolicyOverride.None,
            " ",
            TestContext.Current.CancellationToken);

        Error refused = replaced.Match(() => throw new Xunit.Sdk.XunitException("replaced"), error => error);

        Assert.Equal(ErrorCodes.ConfigurationChangeReasonRequired, refused.Code);
        Assert.Equal(PolicyKey(Branch), refused.Details["key"].GetString());
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// IDN-ORG-003 and 09 section 8a: reading or replacing the policy of an organization
    /// the deployment does not hold is <c>404</c> <c>identity.organization.notfound</c>,
    /// and nothing is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_003_ThePolicyOfAnOrganizationTheDeploymentDoesNotHoldIsNotFoundAsync()
    {
        Browser administrator = await AuthorisedAsync();
        var unheld = new OrganizationId(Guid.NewGuid());

        Answer read = await administrator.SendAsync("GET", PathOf(unheld));
        Answer replaced = await administrator.SendAsync("PUT", PathOf(unheld), Tightened);

        Assert.Equal(StatusCodes.Status404NotFound, read.Status);
        Assert.Equal(ErrorCodes.OrganizationNotFound.ToString(), read.Text("code"));
        Assert.Equal(StatusCodes.Status404NotFound, replaced.Status);
        Assert.Equal(ErrorCodes.OrganizationNotFound.ToString(), replaced.Text("code"));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// 09 section 8a and 10: an organization's policy is <c>organization:manage</c> in
    /// the administrative organization; the permission held in the organization itself
    /// is not enough to read it or to change it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002a_ThePolicyIsGovernedFromTheAdministrativeOrganizationAsync()
    {
        (Browser administrator, SubjectId subject) = await SignedInAsync();

        _deployment.Gate.Grant(subject, Branch, Permissions.OrganizationManage);

        Answer read = await administrator.SendAsync("GET", PathOf(Branch));
        Answer replaced = await administrator.SendAsync("PUT", PathOf(Branch), Tightened);

        Assert.Equal(StatusCodes.Status403Forbidden, read.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), read.Text("code"));
        Assert.Equal(StatusCodes.Status403Forbidden, replaced.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), replaced.Text("code"));
        Assert.Empty(_deployment.Changes.Written);
    }

    /// <summary>
    /// IDN-ATTR-002 AC4, OPS-CFG-003 AC4: where the host declares no image codec, a
    /// change that turns photos on is refused naming the field and the declaration it
    /// needs, for an organization's policy and for the system's alike, and nothing is
    /// written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_002_AC4_PhotosAreNotTurnedOnWithoutACodecAsync()
    {
        await using var bare = new Janus.Hosting.Tests.Deployment(codec: false);

        Flow.Prepare(bare);
        bare.Administers(Administration);
        bare.Organizations.Seed(Branch);

        Browser administrator = await Flow.SignedInAsync(bare);
        SubjectId subject = bare.Directory.Created[^1].Subject;

        bare.Gate.Grant(subject, Administration, Permissions.OrganizationManage);
        bare.Gate.Grant(subject, Administration, Permissions.ConfigurationManage);
        bare.Gate.Grant(subject, Administration, Permissions.SystemAdminister);

        Answer organization = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"photos":true,"reason":"Staff show their faces."}""");
        Answer system = await administrator.SendAsync(
            "PUT",
            "/admin/config/policy.default",
            """{"value":{"requiredAssurance":"aal1","loginFactors":["passkey","password"],"gates":{},"credentialRedundancy":"advisory","selfServiceRecovery":true,"emailDomains":[],"photos":true},"reason":"Everyone shows a face."}""");

        foreach (Answer refused in new[] { organization, system })
        {
            JsonElement details = refused.Json().GetProperty("details");

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
            Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed.ToString(), refused.Text("code"));
            Assert.Equal("photos", details.GetProperty("field").GetString());
            Assert.Equal("imageCodec", details.GetProperty("requires").GetString());
        }

        Assert.Empty(bare.Changes.Written);
        Assert.False(
            (await administrator.SendAsync("GET", PathOf(Branch)))
                .Json()
                .GetProperty("photos")
                .GetProperty("value")
                .GetBoolean());
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the administrator committed after the gate
    /// step and before the first write refuses the replacing of a policy, which stays as
    /// it stood.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesAPolicyChangeAsync()
    {
        Browser administrator = await AuthorisedAsync();

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => administrator.SendAsync("PUT", PathOf(Branch), Tightened));

        Assert.Empty(_deployment.Changes.Written);
        Assert.Null((await OverrideAsync(Branch)).RequiredAssurance);
    }

    private static string PathOf(OrganizationId organization) => "/admin/organizations/" + organization + "/policy";

    private static string PolicyKey(OrganizationId organization) =>
        Settings.OrganizationPolicy.For(organization.ToString()).ToString();

    private static string Unreasoned(Answer answer)
    {
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, answer.Status);
        Assert.Equal(ErrorCodes.ConfigurationChangeReasonRequired.ToString(), answer.Text("code"));

        return answer.Json().GetProperty("details").GetProperty("key").GetString()!;
    }

    private static string Member(Answer answer)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);

        return answer.Json().GetProperty("details").GetProperty("member").GetString()!;
    }

    private static string Below(Answer answer)
    {
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, answer.Status);
        Assert.Equal(ErrorCodes.ConfigurationPolicyBelowSystem.ToString(), answer.Text("code"));

        return answer.Json().GetProperty("details").GetProperty("field").GetString()!;
    }

    // The override the deployment holds for the organization.
    private async Task<PolicyOverride> OverrideAsync(OrganizationId organization) =>
        (await _deployment.Configuration.ReadAsync(
            Settings.OrganizationPolicy,
            organization.ToString(),
            CancellationToken.None))
            .Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private async Task<(Browser Browser, SubjectId Subject)> SignedInAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        return (browser, _deployment.Directory.Created[^1].Subject);
    }

    private async Task<Browser> AuthorisedAsync()
    {
        (Browser browser, SubjectId subject) = await SignedInAsync();

        _deployment.Gate.Grant(subject, Administration, Permissions.OrganizationManage);

        return browser;
    }
}
