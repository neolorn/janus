using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The two policies chapter 10 section 4.1a states, field by field, and the one entry
/// of the factor catalogue a policy never admits.
/// </summary>
[Trait("kind", "unit")]
public sealed class PolicyTests
{
    /// <summary>
    /// Chapter 10 section 4.1a: the system policy is the catalogue less the four
    /// entries that are off until a host enables them, one factor, a gate at the
    /// account's reachable assurance, a second credential offered, self-service
    /// recovery available and no domain lock.
    /// </summary>
    [Fact]
    public void SystemDefault_EveryField_IsWhatSectionFourOneAStates()
    {
        Policy policy = Policies.SystemDefault;

        Assert.Equal(AssuranceLevel.Aal1, policy.RequiredAssurance);
        Assert.Equal(
            new[]
            {
                Factor.Password,
                Factor.Passkey,
                Factor.Google,
                Factor.Apple,
                Factor.Totp,
                Factor.SecurityKey,
                Factor.RecoveryCodes,
            }.Order(),
            policy.LoginFactors.Order());
        Assert.Equal(CredentialRedundancy.Advisory, policy.CredentialRedundancy);
        Assert.True(policy.SelfServiceRecovery);
        Assert.Empty(policy.EmailDomains);
    }

    /// <summary>
    /// Chapter 10 section 4.1a: the administrative organization is created at
    /// bootstrap with two factors, passkeys only, a second credential required and no
    /// self-service recovery.
    /// </summary>
    [Fact]
    public void AdministrativeOrganization_EveryField_IsWhatSectionFourOneAStates()
    {
        Policy policy = Policies.AdministrativeOrganization;

        Assert.Equal(AssuranceLevel.Aal2, policy.RequiredAssurance);
        Assert.Equal([Factor.Passkey], policy.LoginFactors);
        Assert.Equal(CredentialRedundancy.Enforced, policy.CredentialRedundancy);
        Assert.False(policy.SelfServiceRecovery);
        Assert.Empty(policy.EmailDomains);
    }

    /// <summary>
    /// AUTH-STEP-002a AC3: the gate values for every action are readable from the
    /// resolved policy, so neither default may leave an action ungated.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002a_AC3_TheSystemPolicyGatesEveryStepUpAction() =>
        Assert.Equal(
            Enum.GetValues<StepUpAction>().Order(),
            Policies.SystemDefault.Gates.Keys.Order());

    /// <summary>
    /// AUTH-STEP-002a AC3, for the policy staff resolve to.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002a_AC3_TheAdministrativePolicyGatesEveryStepUpAction() =>
        Assert.Equal(
            Enum.GetValues<StepUpAction>().Order(),
            Policies.AdministrativeOrganization.Gates.Keys.Order());

    /// <summary>
    /// Chapter 10 section 4.1a: under the system policy every gate asks for the
    /// account's reachable assurance without phishing-resistance, within the step-up
    /// recency.
    /// </summary>
    [Fact]
    public void SystemDefault_EveryGate_IsReachableWithoutPhishingResistance() =>
        Assert.All(
            Policies.SystemDefault.Gates.Values,
            gate => Assert.Equal(
                new Gate(GateLevel.Reachable, PhishingResistant: false, Settings.SessionStepUpRecency.Default),
                gate));

    /// <summary>
    /// Chapter 10 section 4.1a: under the administrative organization's policy every
    /// gate asks for AAL2 with phishing-resistance.
    /// </summary>
    [Fact]
    public void AdministrativeOrganization_EveryGate_IsAal2AndPhishingResistant() =>
        Assert.All(
            Policies.AdministrativeOrganization.Gates.Values,
            gate => Assert.Equal(
                new Gate(GateLevel.Aal2, PhishingResistant: true, Settings.SessionStepUpRecency.Default),
                gate));

    /// <summary>
    /// Chapter 10 section 4.1a states two levels for this field. A policy asking for
    /// AAL3 would state a floor no factor the library holds can reach, and one asking
    /// for delegated would state a floor below a single factor.
    /// </summary>
    [Theory]
    [InlineData(AssuranceLevel.Aal3)]
    [InlineData(AssuranceLevel.Delegated)]
    public void RequiredAssurance_ALevelOutsideTheField_Refused(AssuranceLevel level) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Policies.SystemDefault with { RequiredAssurance = level });

    /// <summary>
    /// AUTH-FACT-002: the emergency credential satisfies every gate for the session's
    /// lifetime, so it is never one of the factors a person signs in with.
    /// </summary>
    [Fact]
    public void LoginFactors_TheEmergencyCredential_Refused() =>
        Assert.Throws<ArgumentException>(() =>
            Policies.SystemDefault with
            {
                LoginFactors = new[] { Factor.Password, Factor.BreakGlass }.ToFrozenSet(),
            });
}
