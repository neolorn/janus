using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            VerificationOnly: true,
            SignInOnly: false,
            IsWebAuthn: false,
            IsDiscoverable: false,
            Channel: null,
            Restricted: false,
            SingleUse: false);

        Assert.Null(Assurance.Reached([channel]));
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Assurance.Reached([FactorCatalogue.Of(Factor.Password), channel]));
    }

    /// <summary>
    /// AUTH-FACT-003 AC2 and AC3: a link alone signs in at AAL1 and proves nothing
    /// afterwards, whichever channel carried it.
    /// </summary>
    /// <param name="factor">The entry.</param>
    [Theory]
    [InlineData(Factor.EmailLink)]
    [InlineData(Factor.EmailCode)]
    [InlineData(Factor.PhoneLink)]
    public void AUTH_FACT_003_AC2_AnEmailFactorSignsInAtAal1AndProvesNothingAfterwards(Factor factor)
    {
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Assurance.Reached(Properties([factor])));
        Assert.Null(Assurance.Proved(Properties([factor])));
    }

    /// <summary>
    /// AUTH-FACT-003 AC3: the factor beside an email factor is what counts
    /// afterwards, the email factor itself contributing nothing to it.
    /// </summary>
    [Fact]
    public void AUTH_FACT_003_AC3_AnEmailFactorRaisesNothingBesideASecondFactor() =>
        Assert.Null(Assurance.Proved(Properties([Factor.EmailCode, Factor.Totp])));

    /// <summary>
    /// AUTH-STEP-002: what a combination of the account's own factors proves on a
    /// session that already exists is the same arithmetic as at sign-in.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC4_AnOwnCombinationProvesWhatItReaches() =>
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            Assurance.Proved(Properties([Factor.Password, Factor.SecurityKey])));

    /// <summary>
    /// AUTH-SESS-005a AC2: a session opened on a provider's word alone records no
    /// tier of ours, whichever provider it was.
    /// </summary>
    /// <param name="provider">The provider.</param>
    [Theory]
    [InlineData(Factor.Google)]
    [InlineData(Factor.Apple)]
    public void AUTH_SESS_005a_AC2_ASocialOnlySessionRecordsDelegated(Factor provider) =>
        Assert.Equal(
            new Assurance(AssuranceLevel.Delegated, PhishingResistant: false),
            Assurance.Reached(Properties([provider])));

    /// <summary>
    /// AUTH-SESS-005a AC2b and AUTH-FACT-002 AC5: a password and a code sent by SMS
    /// reach the second tier and resist no relay.
    /// </summary>
    [Fact]
    public void AUTH_SESS_005a_AC2b_APasswordAndAnSmsCodeReachAal2WithoutRelayResistance() =>
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: false),
            Assurance.Reached(Properties([Factor.Password, Factor.PhoneCode])));

    /// <summary>
    /// AUTH-SESS-005a AC3: the arithmetic reads the catalogue's properties and has no
    /// other input, so nothing an external provider asserts can raise a tier.
    /// </summary>
    [Fact]
    public void AUTH_SESS_005a_AC3_NoLevelIsDerivedFromAnExternalClaim()
    {
        ParameterInfo only = Assert.Single(
            typeof(Assurance).GetMethod(nameof(Assurance.Reached))!.GetParameters());

        Assert.Equal(typeof(IReadOnlyCollection<FactorProperties>), only.ParameterType);
        Assert.Equal(
            Assurance.Reached(Properties([Factor.Google])),
            Assurance.Reached(Properties([Factor.Apple])));
    }

    /// <summary>
    /// AUTH-FACT-002 AC5: a link sent by SMS signs in at the first tier on its own.
    /// </summary>
    [Fact]
    public void AUTH_FACT_002_AC5_APhoneLinkAloneRecordsAal1() =>
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Assurance.Reached(Properties([Factor.PhoneLink])));

    /// <summary>
    /// AUTH-FACT-001 AC2: an entry registered as relay-resistant reaches what its
    /// properties say and satisfies the rules that read them, none of which changes
    /// to admit it.
    /// </summary>
    [Fact]
    public void AUTH_FACT_001_AC2_ANewRelayResistantFactorSatisfiesTheRulesUnchanged()
    {
        var registered = new FactorProperties(
            CanBePrimary: true,
            CanBeSecondFactor: true,
            IsPhishingResistant: true,
            AssuranceLevel.Aal2,
            VerificationOnly: false,
            SignInOnly: false,
            IsWebAuthn: false,
            IsDiscoverable: true,
            Channel: null,
            Restricted: false,
            SingleUse: false);

        Assert.Equal(
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            Assurance.Reached([registered]));
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            Assurance.Proved([registered]));
    }

    /// <summary>
    /// AUTH-SESS-002 AC3: a step-up rule carries a tier, whether relay resistance is
    /// asked and how recently it was proved, and no field for a factor.
    /// </summary>
    [Fact]
    public void AUTH_SESS_002_AC3_AStepUpRuleIsALevelAResistanceAndAnAge()
    {
        string[] held = [.. typeof(Gate).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal)];

        Assert.Equal(["Level", "MaximumAge", "PhishingResistant"], held);
        Assert.DoesNotContain(
            typeof(Gate).GetProperties(),
            property => property.PropertyType == typeof(Factor));
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
