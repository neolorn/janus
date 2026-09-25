using System;
using System.Collections.Generic;
using Janus.Authentication.Policies;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Policies;

/// <summary>
/// How a replacement of an organization's policy is judged against the system policy
/// and against what it replaces (AUTH-STEP-002a, OPS-CFG-002, D-143).
/// </summary>
[Trait("kind", "unit")]
public sealed class PolicyStrictnessTests
{
    private static readonly Gate Reachable = new(GateLevel.Reachable, PhishingResistant: false, TimeSpan.FromMinutes(15));

    private static readonly Gate Strict = new(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5));

    // A system policy at the strict end of every field an organization could loosen.
    private static readonly Policy StrictSystem = Janus.Core.Policies.SystemDefault with
    {
        RequiredAssurance = AssuranceLevel.Aal2,
        LoginFactors = new HashSet<Factor> { Factor.Passkey, Factor.Totp },
        CredentialRedundancy = CredentialRedundancy.Enforced,
        SelfServiceRecovery = false,
    };

    /// <summary>
    /// AUTH-STEP-002a: each field an organization states looser than the system policy
    /// is named.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002a_AFieldStatedLooserThanTheSystemIsNamed()
    {
        Assert.Equal(
            "requiredAssurance",
            PolicyStrictness.BelowSystem(StrictSystem, PolicyOverride.None with { RequiredAssurance = AssuranceLevel.Aal1 }));
        Assert.Equal(
            "loginFactors",
            PolicyStrictness.BelowSystem(
                StrictSystem,
                PolicyOverride.None with { LoginFactors = new HashSet<Factor> { Factor.Passkey, Factor.Password } }));
        Assert.Equal(
            "gates",
            PolicyStrictness.BelowSystem(
                StrictSystem with { Gates = GatesOf(Strict) },
                PolicyOverride.None with { Gates = new Dictionary<StepUpAction, Gate> { [StepUpAction.PolicyChange] = Reachable } }));
        Assert.Equal(
            "credentialRedundancy",
            PolicyStrictness.BelowSystem(
                StrictSystem,
                PolicyOverride.None with { CredentialRedundancy = CredentialRedundancy.Advisory }));
        Assert.Equal(
            "selfServiceRecovery",
            PolicyStrictness.BelowSystem(StrictSystem, PolicyOverride.None with { SelfServiceRecovery = true }));
    }

    /// <summary>
    /// AUTH-STEP-002a: an override that states nothing, or states each field as strict
    /// as the system policy or stricter, is allowed.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002a_AnOverrideNoLooserThanTheSystemIsAllowed()
    {
        var tighter = new PolicyOverride(
            AssuranceLevel.Aal2,
            new HashSet<Factor> { Factor.Passkey },
            new Dictionary<StepUpAction, Gate> { [StepUpAction.PolicyChange] = Strict },
            CredentialRedundancy.Enforced,
            SelfServiceRecovery: false,
            EmailDomains: null);

        Assert.Null(PolicyStrictness.BelowSystem(StrictSystem, PolicyOverride.None));
        Assert.Null(PolicyStrictness.BelowSystem(StrictSystem, tighter));
    }

    /// <summary>
    /// OPS-CFG-002: a change is a loosening where any field grants more than it did,
    /// and not where every field grants the same or less, whatever order a set is
    /// written in.
    /// </summary>
    [Fact]
    public void OPS_CFG_002_AChangeIsALooseningWhereAnyFieldGrantsMore()
    {
        Policy before = StrictSystem with { Gates = GatesOf(Strict), EmailDomains = ["a.example"] };

        Assert.True(PolicyStrictness.Loosens(before, before with { RequiredAssurance = AssuranceLevel.Aal1 }));
        Assert.True(PolicyStrictness.Loosens(
            before,
            before with { LoginFactors = new HashSet<Factor> { Factor.Passkey, Factor.Totp, Factor.Password } }));
        Assert.True(PolicyStrictness.Loosens(before, before with { Gates = GatesOf(Reachable) }));
        Assert.True(PolicyStrictness.Loosens(before, before with { CredentialRedundancy = CredentialRedundancy.Advisory }));
        Assert.True(PolicyStrictness.Loosens(before, before with { SelfServiceRecovery = true }));
        Assert.True(PolicyStrictness.Loosens(before, before with { EmailDomains = [] }));
        Assert.True(PolicyStrictness.Loosens(before, before with { EmailDomains = ["a.example", "b.example"] }));
        Assert.False(PolicyStrictness.Loosens(before, before));
        Assert.False(PolicyStrictness.Loosens(
            before,
            before with { LoginFactors = new HashSet<Factor> { Factor.Totp, Factor.Passkey } }));
        Assert.False(PolicyStrictness.Loosens(before, before with { LoginFactors = new HashSet<Factor> { Factor.Passkey } }));
    }

    /// <summary>
    /// D-143: a field the organization states is its own while its value is in force,
    /// and inherited once the system policy has overtaken it; a field it does not
    /// state is inherited.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002a_AStatedFieldIsTheOrganizationsOwnWhileItIsInForce()
    {
        Policy system = Janus.Core.Policies.SystemDefault;
        var stated = new PolicyOverride(
            AssuranceLevel.Aal2,
            new HashSet<Factor> { Factor.Passkey, Factor.Password },
            new Dictionary<StepUpAction, Gate> { [StepUpAction.PolicyChange] = Strict },
            CredentialRedundancy: null,
            SelfServiceRecovery: null,
            EmailDomains: null);
        Policy raised = system with
        {
            LoginFactors = new HashSet<Factor> { Factor.Passkey },
            Gates = GatesOf(Strict with { MaximumAge = TimeSpan.FromMinutes(1) }),
        };

        PolicyOverride own = PolicyStrictness.InForce(system, PolicyStrictness.Tighten(system, stated), stated);
        PolicyOverride overtaken = PolicyStrictness.InForce(raised, PolicyStrictness.Tighten(raised, stated), stated);

        Assert.Equal(AssuranceLevel.Aal2, own.RequiredAssurance);
        Assert.True(own.LoginFactors!.SetEquals([Factor.Password, Factor.Passkey]));
        Assert.Equal(Strict, Assert.Single(own.Gates!).Value);
        Assert.Null(own.CredentialRedundancy);
        Assert.Null(own.SelfServiceRecovery);
        Assert.Null(own.EmailDomains);
        Assert.Equal(AssuranceLevel.Aal2, overtaken.RequiredAssurance);
        Assert.Null(overtaken.LoginFactors);
        Assert.Null(overtaken.Gates);
    }

    private static Dictionary<StepUpAction, Gate> GatesOf(Gate gate)
    {
        Dictionary<StepUpAction, Gate> gates = [];

        foreach (StepUpAction action in Enum.GetValues<StepUpAction>())
        {
            gates[action] = gate;
        }

        return gates;
    }
}
