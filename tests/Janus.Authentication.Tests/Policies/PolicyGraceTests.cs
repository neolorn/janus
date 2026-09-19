using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Authentication.Policies;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Policies;

/// <summary>
/// What a raised requirement does to the sign-ins of the accounts under it
/// (AUTH-FACT-017).
/// </summary>
[Trait("kind", "unit")]
public sealed class PolicyGraceTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// AUTH-FACT-017 AC1: with no run-up, a member who does not meet the raised
    /// requirement is held at enrolment on their next sign-in.
    /// </summary>
    [Fact]
    public void AUTH_FACT_017_AC1_WithNoRunUpTheRequirementHoldsAtOnce()
    {
        PolicyHold held = Held(TimeSpan.Zero, complies: false, Noon - TimeSpan.FromDays(400), Noon);

        Assert.True(held.Expired);
        Assert.Equal(PolicyField.RequiredAssurance, held.Requirement.Field);
        Assert.Equal("aal2", held.Requirement.Value);
        Assert.Equal(Noon, held.Requirement.Deadline);
    }

    /// <summary>
    /// AUTH-FACT-017 AC2: inside the run-up the sign-in carries the requirement and
    /// the deadline and continues; after it, it stops at enrolment.
    /// </summary>
    [Fact]
    public void AUTH_FACT_017_AC2_InsideTheRunUpTheSignInContinues()
    {
        PolicyHold inside = Held(
            TimeSpan.FromDays(30),
            complies: false,
            Noon - TimeSpan.FromDays(400),
            Noon + TimeSpan.FromDays(29));

        Assert.False(inside.Expired);
        Assert.Equal(Noon + TimeSpan.FromDays(30), inside.Requirement.Deadline);

        PolicyHold after = Held(
            TimeSpan.FromDays(30),
            complies: false,
            Noon - TimeSpan.FromDays(400),
            Noon + TimeSpan.FromDays(31));

        Assert.True(after.Expired);
    }

    /// <summary>
    /// AUTH-FACT-017 AC3: an account created after the change was created under the
    /// new requirement and is held at its first sign-in, run-up or no run-up.
    /// </summary>
    [Fact]
    public void AUTH_FACT_017_AC3_AnAccountCreatedAfterTheChangeIsHeldAtOnce()
    {
        PolicyHold held = Held(
            TimeSpan.FromDays(30),
            complies: false,
            Noon + TimeSpan.FromDays(1),
            Noon + TimeSpan.FromDays(1));

        Assert.True(held.Expired);
        Assert.Equal(Noon, held.Requirement.Deadline);
    }

    /// <summary>
    /// AUTH-FACT-017 AC2: an account that meets the requirement is told nothing and
    /// held by nothing.
    /// </summary>
    [Fact]
    public void AUTH_FACT_017_AC2_AnAccountThatMeetsItIsHeldByNothing() =>
        Assert.Null(PolicyGrace.On(
            Raise(),
            TimeSpan.FromDays(30),
            complies: true,
            Noon - TimeSpan.FromDays(400),
            Noon + TimeSpan.FromDays(31)));

    /// <summary>
    /// AUTH-FACT-017 AC4: lowering a requirement raises nothing, so it produces no
    /// run-up and holds nobody.
    /// </summary>
    [Fact]
    public void AUTH_FACT_017_AC4_LoweringARequirementRaisesNothing() =>
        Assert.Empty(PolicyGrace.Raised(
            Policy(AssuranceLevel.Aal2, CredentialRedundancy.Enforced),
            Policy(AssuranceLevel.Aal1, CredentialRedundancy.Advisory),
            Noon));

    /// <summary>
    /// AUTH-FACT-017: the two fields that can be raised are the two the change is
    /// read for, each carrying what it was raised to.
    /// </summary>
    [Fact]
    public void AUTH_FACT_017_BothFieldsOfAChangeAreRead()
    {
        IReadOnlyList<PolicyRaise> raised = PolicyGrace.Raised(
            Policy(AssuranceLevel.Aal1, CredentialRedundancy.Advisory),
            Policy(AssuranceLevel.Aal2, CredentialRedundancy.Enforced),
            Noon);

        Assert.Equal(
            [PolicyField.RequiredAssurance, PolicyField.CredentialRedundancy],
            raised.Select(raise => raise.Field));
        Assert.Equal(["aal2", "enforced"], raised.Select(raise => raise.Value));
        Assert.All(raised, raise => Assert.Equal(Noon, raise.At));
    }

    /// <summary>
    /// AUTH-FACT-017: a change that leaves both fields where they stood raises
    /// nothing.
    /// </summary>
    [Fact]
    public void AUTH_FACT_017_AChangeToNeitherFieldRaisesNothing() =>
        Assert.Empty(PolicyGrace.Raised(
            Policy(AssuranceLevel.Aal1, CredentialRedundancy.Advisory),
            Policy(AssuranceLevel.Aal1, CredentialRedundancy.Advisory),
            Noon));

    private static PolicyRaise Raise() =>
        new(PolicyField.RequiredAssurance, "aal2", Noon);

    private static PolicyHold Held(
        TimeSpan grace,
        bool complies,
        DateTimeOffset registered,
        DateTimeOffset now) =>
        PolicyGrace.On(Raise(), grace, complies, registered, now)
        ?? throw new Xunit.Sdk.XunitException("The requirement holds nothing.");

    private static Policy Policy(AssuranceLevel level, CredentialRedundancy redundancy) =>
        Janus.Core.Policies.SystemDefault with
        {
            RequiredAssurance = level,
            CredentialRedundancy = redundancy,
        };
}
