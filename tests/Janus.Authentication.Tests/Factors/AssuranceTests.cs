using System.Collections.Generic;
using Janus.Authentication.Factors;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// The one arithmetic, over the table of AUTH-SESS-005a.
/// </summary>
[Trait("kind", "unit")]
public sealed class AssuranceTests
{
    /// <summary>
    /// Every row of the assignment table: what was presented, the tier it reaches and
    /// whether it resists credential relay.
    /// </summary>
    public static TheoryData<Factor[], AssuranceLevel, bool> Assignments =>
        new()
        {
            { [Factor.Password], AssuranceLevel.Aal1, false },
            { [Factor.EmailLink], AssuranceLevel.Aal1, false },
            { [Factor.PhoneLink], AssuranceLevel.Aal1, false },
            { [Factor.EmailCode], AssuranceLevel.Aal1, false },
            { [Factor.Password, Factor.PhoneCode], AssuranceLevel.Aal2, false },
            { [Factor.Google], AssuranceLevel.Delegated, false },
            { [Factor.Apple], AssuranceLevel.Delegated, false },
            { [Factor.Passkey], AssuranceLevel.Aal2, true },
            { [Factor.Password, Factor.Totp], AssuranceLevel.Aal2, false },
            { [Factor.Password, Factor.SecurityKey], AssuranceLevel.Aal2, true },
            { [Factor.Password, Factor.RecoveryCodes], AssuranceLevel.Aal2, false },
            { [Factor.Google, Factor.Totp], AssuranceLevel.Delegated, false },
            { [Factor.Google, Factor.SecurityKey], AssuranceLevel.Delegated, false },
            { [Factor.EmailLink, Factor.Totp], AssuranceLevel.Aal2, false },
            { [Factor.EmailCode, Factor.SecurityKey], AssuranceLevel.Aal2, true },
            { [Factor.Passkey, Factor.Password], AssuranceLevel.Aal2, true },
            { [Factor.Password, Factor.SecurityKey, Factor.Totp], AssuranceLevel.Aal2, true },
        };

    /// <summary>
    /// AUTH-SESS-005a AC1, AC2, AC2b; AUTH-FACT-002 AC5: the table assigns one tier
    /// and one phishing-resistance to each combination.
    /// </summary>
    /// <param name="presented">What was presented.</param>
    /// <param name="level">The tier it reaches.</param>
    /// <param name="phishingResistant">Whether it resists credential relay.</param>
    [Theory]
    [MemberData(nameof(Assignments))]
    public void AUTH_SESS_005a_AC1_EveryCombinationReachesWhatTheTableAssigns(
        Factor[] presented,
        AssuranceLevel level,
        bool phishingResistant)
    {
        Assurance? reached = Assurance.Reached(Properties(presented));

        Assert.Equal(new Assurance(level, phishingResistant), reached);
    }

    /// <summary>
    /// AUTH-SESS-005a: a second factor alone contributes nothing, there being no first
    /// factor of ours beside it.
    /// </summary>
    [Fact]
    public void Reached_ASecondFactorAlone_ReachesNothing() =>
        Assert.Null(Assurance.Reached(Properties([Factor.Totp])));

    /// <summary>
    /// AUTH-FACT-001 AC3: a factor that proves control of a channel never
    /// authenticates, whatever else it is registered with.
    /// </summary>
    [Fact]
    public void AUTH_FACT_001_AC3_AVerificationOnlyFactorAuthenticatesNeither()
    {
        var channel = new FactorProperties(
            CanBePrimary: true,
            CanBeSecondFactor: true,
            IsPhishingResistant: false,
            AssuranceLevel.Aal2,
            VerificationOnly: true);

        Assert.Null(Assurance.Reached([channel]));
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Assurance.Reached([FactorCatalogue.Of(Factor.Password), channel]));
    }

    private static FactorProperties[] Properties(Factor[] presented)
    {
        List<FactorProperties> properties = [];

        foreach (Factor factor in presented)
        {
            properties.Add(FactorCatalogue.Of(factor));
        }

        return [.. properties];
    }
}
