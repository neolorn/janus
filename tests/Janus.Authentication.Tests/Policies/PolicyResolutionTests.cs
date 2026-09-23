using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Policies;

/// <summary>
/// The one policy a principal resolves to, read from their organization membership
/// and from nothing else (AUTH-PRIN-002, AUTH-STEP-002a).
/// </summary>
[Trait("kind", "unit")]
public sealed class PolicyResolutionTests : IDisposable
{
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    // The namespace of this file is the area's, so the catalogue of chapter 10
    // section 4.1a is named here rather than qualified at every use.
    private static Policy SystemPolicy => Janus.Core.Policies.SystemDefault;

    private PolicyResolution Resolution => new(_memberships, _configuration, _raises);

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    /// <summary>
    /// AUTH-PRIN-002 AC3: a principal with no membership follows the system policy
    /// and is unaffected by any organization's overrides.
    /// </summary>
    [Fact]
    public async Task AUTH_PRIN_002_AC3_APrincipalWithNoMembershipFollowsTheSystemPolicyAsync()
    {
        Override(Organization(), new PolicyOverride(
            AssuranceLevel.Aal2,
            null,
            null,
            null,
            SelfServiceRecovery: false,
            null));

        Policy resolved = await ResolvedAsync(SubjectId.New(_randomness));

        AssertTheSame(SystemPolicy, resolved);
    }

    /// <summary>
    /// AUTH-PRIN-002 AC4: an organization that overrides nothing behaves exactly as
    /// the system policy.
    /// </summary>
    [Fact]
    public async Task AUTH_PRIN_002_AC4_AnOrganizationOverridingNothingIsTheSystemPolicyAsync()
    {
        var subject = SubjectId.New(_randomness);
        OrganizationId organization = Organization();

        _memberships.Place(subject, organization);
        Override(organization, PolicyOverride.None);

        AssertTheSame(SystemPolicy, await ResolvedAsync(subject));
    }

    /// <summary>
    /// AUTH-PRIN-002 AC2: two organizations with differing policies are enforced
    /// independently in one deployment.
    /// </summary>
    [Fact]
    public async Task AUTH_PRIN_002_AC2_TwoOrganizationsAreEnforcedIndependentlyAsync()
    {
        var staff = SubjectId.New(_randomness);
        var other = SubjectId.New(_randomness);
        OrganizationId administrative = Organization();
        OrganizationId ordinary = Organization();

        _memberships.Place(staff, administrative);
        _memberships.Place(other, ordinary);
        Override(administrative, new PolicyOverride(
            AssuranceLevel.Aal2,
            new[] { Factor.Passkey }.ToFrozenSet(),
            null,
            CredentialRedundancy.Enforced,
            SelfServiceRecovery: false,
            null));
        Override(ordinary, PolicyOverride.None);

        Policy theirs = await ResolvedAsync(staff);
        Policy others = await ResolvedAsync(other);

        Assert.Equal(AssuranceLevel.Aal2, theirs.RequiredAssurance);
        Assert.Equal([Factor.Passkey], theirs.LoginFactors);
        Assert.False(theirs.SelfServiceRecovery);
        Assert.Equal(AssuranceLevel.Aal1, others.RequiredAssurance);
        Assert.True(others.SelfServiceRecovery);
    }

    /// <summary>
    /// AUTH-PRIN-002: an organization may tighten a field and may not loosen one
    /// below the system default, whenever the value was written.
    /// </summary>
    [Fact]
    public async Task AUTH_PRIN_002_AnOrganizationCannotLoosenBelowTheSystemDefaultAsync()
    {
        var subject = SubjectId.New(_randomness);
        OrganizationId organization = Organization();

        _memberships.Place(subject, organization);
        _configuration.Set(
            Settings.PolicyDefault,
            SystemPolicy with { SelfServiceRecovery = false });
        Override(organization, new PolicyOverride(
            null,
            null,
            null,
            null,
            SelfServiceRecovery: true,
            null));

        Assert.False((await ResolvedAsync(subject)).SelfServiceRecovery);
    }

    /// <summary>
    /// AUTH-STEP-002a and chapter 10 section 4.1a: an organization's gates name only the
    /// actions it tightens, and every other action keeps the system's gate.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002a_AnOverrideNamingSomeGatesKeepsTheSystemsForTheRestAsync()
    {
        var subject = SubjectId.New(_randomness);
        OrganizationId organization = Organization();
        var tightened = new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5));

        _memberships.Place(subject, organization);
        Override(organization, PolicyOverride.None with
        {
            Gates = new Dictionary<StepUpAction, Gate> { [StepUpAction.FactorRemove] = tightened },
        });

        Policy resolved = await ResolvedAsync(subject);

        Assert.Equal(tightened, resolved.Gates[StepUpAction.FactorRemove]);
        Assert.All(
            SystemPolicy.Gates.Where(bound => bound.Key is not StepUpAction.FactorRemove),
            bound => Assert.Equal(bound.Value, resolved.Gates[bound.Key]));
    }

    /// <summary>
    /// AUTH-PRIN-002: a principal holding several memberships follows the strictest
    /// of their organizations' policies, field by field.
    /// </summary>
    [Fact]
    public async Task AUTH_PRIN_002_SeveralMembershipsFollowTheStrictestOfThemAsync()
    {
        var subject = SubjectId.New(_randomness);
        OrganizationId strict = Organization();
        OrganizationId lenient = Organization();

        _memberships.Place(subject, lenient);
        _memberships.Place(subject, strict);
        Override(lenient, new PolicyOverride(
            null,
            new[] { Factor.Password, Factor.Passkey, Factor.Totp }.ToFrozenSet(),
            null,
            null,
            null,
            null));
        Override(strict, new PolicyOverride(
            AssuranceLevel.Aal2,
            new[] { Factor.Passkey, Factor.Totp, Factor.Google }.ToFrozenSet(),
            null,
            CredentialRedundancy.Enforced,
            SelfServiceRecovery: false,
            null));

        Policy resolved = await ResolvedAsync(subject);

        Assert.Equal(AssuranceLevel.Aal2, resolved.RequiredAssurance);
        Assert.Equal<IEnumerable<Factor>>(
            [Factor.Passkey, Factor.Totp],
            [.. resolved.LoginFactors]);
        Assert.Equal(CredentialRedundancy.Enforced, resolved.CredentialRedundancy);
        Assert.False(resolved.SelfServiceRecovery);
    }

    /// <summary>
    /// AUTH-STEP-002a: the strictest gate of several is the one that grants least on
    /// every one of its three values at once.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002a_TheStrictestGateGrantsLeastOnEveryValue()
    {
        var first = new Gate(GateLevel.Reachable, PhishingResistant: true, TimeSpan.FromMinutes(15));
        var second = new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5));

        Gate strictest = PolicyStrictness
            .Strictest(WithGate(first), WithGate(second))
            .Gates[StepUpAction.PasswordSet];

        Assert.Equal(GateLevel.Aal2, strictest.Level);
        Assert.True(strictest.PhishingResistant);
        Assert.Equal(TimeSpan.FromMinutes(5), strictest.MaximumAge);
    }

    /// <summary>
    /// AUTH-STEP-002a: a gate asking for the account's reachable assurance asks at
    /// least as much as one asking for a single factor, that tier being its floor.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002a_ReachableAssuranceAsksAtLeastAsMuchAsASingleFactor()
    {
        Assert.True(PolicyStrictness.Rank(GateLevel.Reachable) > PolicyStrictness.Rank(GateLevel.Aal1));
        Assert.True(PolicyStrictness.Rank(GateLevel.Aal2) > PolicyStrictness.Rank(GateLevel.Reachable));
    }

    /// <summary>
    /// AUTH-PRIN-002: a domain lock admits only what it names, so any lock beats no
    /// lock and two locks admit only what both name.
    /// </summary>
    [Fact]
    public void AUTH_PRIN_002_TwoDomainLocksAdmitOnlyWhatBothName()
    {
        Policy neither = SystemPolicy;
        Policy one = SystemPolicy with { EmailDomains = ["a.example", "b.example"] };
        Policy other = SystemPolicy with { EmailDomains = ["b.example", "c.example"] };

        Assert.Equal(
            ["a.example", "b.example"],
            PolicyStrictness.Strictest(neither, one).EmailDomains);
        Assert.Equal(
            ["b.example"],
            PolicyStrictness.Strictest(one, other).EmailDomains);
    }

    /// <summary>
    /// AUTH-STEP-002a AC1: a principal holding no membership resolves to the system
    /// policy, whose login rule and gates are the ones it is judged by.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002a_AC1_APrincipalWithNoMembershipResolvesToTheSystemPolicyAsync()
    {
        Policy resolved = await ResolvedAsync(SubjectId.New(_randomness));

        Assert.Equal<IEnumerable<Factor>>([.. SystemPolicy.LoginFactors], [.. resolved.LoginFactors]);
        Assert.Equal(SystemPolicy.Gates, resolved.Gates);
    }

    /// <summary>
    /// AUTH-STEP-002a AC2: tightening the administrative organization's login rule
    /// and its gates leaves a customer where they stood, the two being resolved from
    /// the principal's own membership.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002a_AC2_TheAdministrativeOrganizationsPolicyDoesNotReachACustomerAsync()
    {
        var customer = SubjectId.New(_randomness);
        OrganizationId administrative = Organization();

        _memberships.Place(SubjectId.New(_randomness), administrative);

        Policy before = await ResolvedAsync(customer);

        Override(administrative, new PolicyOverride(
            AssuranceLevel.Aal2,
            new[] { Factor.Passkey }.ToFrozenSet(),
            Enum.GetValues<StepUpAction>().ToFrozenDictionary(
                action => action,
                _ => new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5))),
            CredentialRedundancy.Enforced,
            SelfServiceRecovery: false,
            null));

        AssertTheSame(before, await ResolvedAsync(customer));
        AssertTheSame(SystemPolicy, await ResolvedAsync(customer));
    }

    // A policy's collections are compared by reference by the record it is, so two
    // policies saying the same thing are compared field by field.
    private static void AssertTheSame(Policy expected, Policy actual)
    {
        Assert.Equal(expected.RequiredAssurance, actual.RequiredAssurance);
        Assert.Equal<IEnumerable<Factor>>([.. expected.LoginFactors], [.. actual.LoginFactors]);
        Assert.Equal(expected.Gates, actual.Gates);
        Assert.Equal(expected.CredentialRedundancy, actual.CredentialRedundancy);
        Assert.Equal(expected.SelfServiceRecovery, actual.SelfServiceRecovery);
        Assert.Equal(expected.EmailDomains, actual.EmailDomains);
    }

    private static Policy WithGate(Gate gate) =>
        SystemPolicy with
        {
            Gates = new Dictionary<StepUpAction, Gate>(
                Enum.GetValues<StepUpAction>().ToFrozenDictionary(action => action, _ => gate)),
        };

    private static OrganizationId Organization() => OrganizationId.New(TimeProvider.System);

    private void Override(OrganizationId organization, PolicyOverride overrides) =>
        _configuration.Set(Settings.OrganizationPolicy, organization.ToString(), overrides);

    private async ValueTask<Policy> ResolvedAsync(SubjectId subject) =>
        (await Resolution.ForAsync(subject, TestContext.Current.CancellationToken))
            .Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));
}
