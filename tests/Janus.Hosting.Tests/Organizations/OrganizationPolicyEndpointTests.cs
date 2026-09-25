using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
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
    /// Chapter 10 section 4.1a and API-CONV-002: a replacement names only the fields of
    /// the policy object, never the domain lock, with values the object takes, a reason
    /// and an organization the deployment holds; anything else is refused, naming the
    /// member where it is the request that is malformed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002a_WhatAReplacementNamesMustBeReadableAsync()
    {
        Browser administrator = await AuthorisedAsync();

        Answer misspelt = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"requiredAssurence":"aal2","reason":"Staff hold administrative roles."}""");
        Answer locked = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"emailDomains":["example.com"],"reason":"Staff only."}""");
        Answer unreasoned = await administrator.SendAsync("PUT", PathOf(Branch), """{"requiredAssurance":"aal2"}""");
        Answer numbered = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"requiredAssurance":"aal2","reason":7}""");
        Answer unheld = await administrator.SendAsync("PUT", PathOf(new OrganizationId(Guid.NewGuid())), Tightened);
        Answer unknown = await administrator.SendAsync("GET", PathOf(new OrganizationId(Guid.NewGuid())));
        Answer listed = await administrator.SendAsync("PUT", PathOf(Branch), "[]");
        Answer unreadable = await administrator.SendAsync(
            "PUT",
            PathOf(Branch),
            """{"requiredAssurance":"aal9","reason":"Staff hold administrative roles."}""");

        Assert.Equal("requiredAssurence", Member(misspelt));
        Assert.Equal("emailDomains", Member(locked));
        Assert.Equal("reason", Member(unreasoned));
        Assert.Equal("reason", Member(numbered));
        Assert.Equal("id", Member(unheld));
        Assert.Equal("id", Member(unknown));
        Assert.Equal(StatusCodes.Status400BadRequest, listed.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), listed.Text("code"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unreadable.Status);
        Assert.Equal(ErrorCodes.ConfigurationValueNotAllowed.ToString(), unreadable.Text("code"));
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

    private static string PathOf(OrganizationId organization) => "/admin/organizations/" + organization + "/policy";

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
