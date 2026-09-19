using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What a setting does with a value the deployment names: it refuses one outside the
/// constraints the chapter declares rather than clamping it (OPS-CFG-003), and it
/// hands back the safe default where the deployment names none (LIB-HOST-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class SettingsTests
{
    /// <summary>
    /// OPS-CFG-003 AC1: the floor is enforced in code, and a value under it is
    /// rejected rather than raised to it.
    /// </summary>
    [Fact]
    public void OPS_CFG_003_AC1_APasswordFloorBelowTheStandardsMinimumIsRejected()
    {
        Result<int> outcome = Settings.PasswordFloorSingleFactor.Accept(14);

        Assert.Equal(
            ErrorCodes.ConfigurationValueBelowFloor,
            outcome.Match(_ => default, failure => failure.Code));
    }

    /// <summary>
    /// OPS-CFG-003 AC2: the ceiling is enforced the same way, and the failure names
    /// which end the value crossed.
    /// </summary>
    [Fact]
    public void OPS_CFG_003_AC2_ASessionAbsoluteTimeoutAboveTheMaximumIsRejected()
    {
        Result<TimeSpan> outcome = Settings.SessionDefaultAbsolute.Accept(TimeSpan.FromDays(366));

        Assert.Equal(
            ErrorCodes.ConfigurationValueAboveCeiling,
            outcome.Match(_ => default, failure => failure.Code));
    }

    /// <summary>
    /// AUTH-SESS-005 AC5: the absolute maximum of a session at the higher tier is a
    /// day, so forty-eight hours is refused by name rather than clamped.
    /// </summary>
    [Fact]
    public void AUTH_SESS_005_AC5_AnAal2AbsoluteOfFortyEightHoursIsRejected()
    {
        Result<TimeSpan> outcome = Settings.SessionAal2Absolute.Accept(TimeSpan.FromHours(48));

        Assert.Equal(
            ErrorCodes.ConfigurationValueAboveCeiling,
            outcome.Match(_ => default, failure => failure.Code));
        Assert.Equal(
            "P1D",
            outcome.Match(
                _ => throw new InvalidOperationException(),
                failure => failure.Details)["ceiling"].GetString());
    }

    /// <summary>
    /// OPS-CFG-003 AC1: the failure carries the key and the bound, so the management
    /// application can say which value would be accepted.
    /// </summary>
    [Fact]
    public void Accept_BelowFloor_CarriesTheKeyAndTheBound()
    {
        Result<int> outcome = Settings.PasswordMaximum.Accept(32);

        IReadOnlyDictionary<string, System.Text.Json.JsonElement> details =
            outcome.Match(_ => throw new InvalidOperationException(), failure => failure.Details);

        Assert.Equal("password.maximum", details["key"].GetString());
        Assert.Equal("64", details["floor"].GetString());
    }

    /// <summary>
    /// A value inside both bounds is accepted unchanged.
    /// </summary>
    [Fact]
    public void Accept_InsideTheBounds_ReturnsTheValue()
    {
        Result<int> outcome = Settings.PasswordMaximum.Accept(256);

        Assert.Equal(256, outcome.Match(value => value, _ => 0));
    }

    /// <summary>
    /// A value outside the key's stated set carries the code that names the set
    /// rather than either bound code.
    /// </summary>
    [Fact]
    public void Accept_OutsideTheStatedSet_Refused()
    {
        Result<AttributeRequirement> outcome =
            Settings.RegistrationPhone.Accept(AttributeRequirement.Off);

        Assert.Equal(
            ErrorCodes.ConfigurationValueNotAllowed,
            outcome.Match(_ => default, failure => failure.Code));
    }

    /// <summary>
    /// A set that drops a member the chapter holds in place is refused.
    /// </summary>
    [Fact]
    public void Accept_DroppingAnUnremovableMember_Refused()
    {
        Result<IReadOnlySet<BlocklistRejectionSource>> outcome =
            Settings.PasswordBlocklistSources.Accept(
                new[] { BlocklistRejectionSource.Dictionary }.ToFrozenSet());

        Assert.Equal(
            ErrorCodes.ConfigurationValueNotAllowed,
            outcome.Match(_ => default, failure => failure.Code));
    }

    /// <summary>
    /// AUTH-FACT-014 AC5: the default allow-list is exactly the three algorithms, in
    /// the preference order the chapter states, and a list that drops the one it
    /// holds in place is refused.
    /// </summary>
    [Fact]
    public void AUTH_FACT_014_AC5_TheAllowListHoldsItsThreeAlgorithmsAndKeepsOne()
    {
        Assert.Equal([-8, -7, -257], Settings.WebAuthnAlgorithms.Default);

        Result<IReadOnlyList<int>> outcome = Settings.WebAuthnAlgorithms.Accept([-8, -257]);

        Assert.Equal(
            ErrorCodes.ConfigurationValueNotAllowed,
            outcome.Match(_ => default, failure => failure.Code));
    }

    /// <summary>
    /// A destination list the deployment empties is refused: the deployment names at
    /// least one.
    /// </summary>
    [Fact]
    public void Accept_EmptyDestinationList_Refused()
    {
        Result<IReadOnlyList<string>> outcome = Settings.AlertingEmailDestinations.Accept([]);

        Assert.Equal(
            ErrorCodes.ConfigurationValueNotAllowed,
            outcome.Match(_ => default, failure => failure.Code));
    }

    /// <summary>
    /// The two size keys of chapter 10 section 4 are bytes, and a value above the
    /// ceiling is refused rather than truncated.
    /// </summary>
    [Fact]
    public void Accept_ASizeAboveItsCeiling_Refused()
    {
        Assert.Equal(2097152, Settings.PhotoMaxBytes.Default);
        Assert.Equal(8192, Settings.PreferencesMaxSize.Default);
        Assert.Equal(
            ErrorCodes.ConfigurationValueAboveCeiling,
            Code(Settings.PhotoMaxBytes.Accept(10485761)));
        Assert.Equal(
            ErrorCodes.ConfigurationValueAboveCeiling,
            Code(Settings.PreferencesMaxSize.Accept(65537)));
    }

    /// <summary>
    /// An account holds ten verified addresses of each kind by default and never
    /// fewer than one, since the floor is what makes single-address mode expressible.
    /// </summary>
    [Fact]
    public void Accept_AnIdentifierMaximumBelowOne_Refused()
    {
        Assert.Equal(10, Settings.IdentifiersEmailMax.Default);
        Assert.Equal(10, Settings.IdentifiersPhoneMax.Default);
        Assert.Equal(1, Settings.IdentifiersEmailMax.Accept(1).Match(value => value, _ => 0));
        Assert.Equal(
            ErrorCodes.ConfigurationValueBelowFloor,
            Code(Settings.IdentifiersPhoneMax.Accept(0)));
    }

    /// <summary>
    /// The bot-defence signal set is closed at the two members chapter 10 section 4.5
    /// names, both of which are on by default.
    /// </summary>
    [Fact]
    public void Accept_ASignalOutsideTheClosedSet_Refused()
    {
        Assert.Equal(
            new HashSet<BotDefenceSignal>
            {
                BotDefenceSignal.DatacenterRange,
                BotDefenceSignal.RepeatedAttempts,
            },
            Settings.AbuseBotDefenceSignals.Default);
        Assert.Equal(
            ErrorCodes.ConfigurationValueNotAllowed,
            Code(Settings.AbuseBotDefenceSignals.Accept(
                new HashSet<BotDefenceSignal> { (BotDefenceSignal)7 })));
    }

    /// <summary>
    /// PRIV-RIGHT-002: only a deadline shorter than the statutory period is
    /// configurable, so the default is also the ceiling.
    /// </summary>
    [Fact]
    public void Accept_APrivacyDeadlineAboveTheStatutoryPeriod_Refused()
    {
        Assert.Equal(6, Settings.PrivacyRequestDecision.Default);
        Assert.Equal(5, Settings.PrivacyRequestDecision.Accept(5).Match(value => value, _ => 0));
        Assert.Equal(
            ErrorCodes.ConfigurationValueAboveCeiling,
            Code(Settings.PrivacyRequestDecision.Accept(7)));
    }

    /// <summary>
    /// The grace and cooling-off windows of chapter 10 section 4.6 carry a floor, so
    /// none of them can be set to nothing.
    /// </summary>
    [Fact]
    public void Accept_AGraceBelowItsFloor_Refused()
    {
        Assert.Equal(
            ErrorCodes.ConfigurationValueBelowFloor,
            Code(Settings.OrganizationDeletionGrace.Accept(TimeSpan.FromDays(6))));
        Assert.Equal(
            ErrorCodes.ConfigurationValueBelowFloor,
            Code(Settings.AccountDeletionGrace.Accept(TimeSpan.Zero)));
        Assert.Equal(
            ErrorCodes.ConfigurationValueBelowFloor,
            Code(Settings.TakedownGrace.Accept(TimeSpan.FromDays(6))));
        Assert.Equal(
            ErrorCodes.ConfigurationValueBelowFloor,
            Code(Settings.IdentifierChangeCoolingOff.Accept(TimeSpan.FromHours(71))));
        Assert.Equal(
            ErrorCodes.ConfigurationValueBelowFloor,
            Code(Settings.IdentifiersUsernameChangeCoolOff.Accept(TimeSpan.FromHours(23))));
    }

    /// <summary>
    /// A key the deployment has to name has no default to fall back to, so reading
    /// one is a fault rather than a silent empty value.
    /// </summary>
    [Fact]
    public void Default_KeyTheDeploymentNames_Throws() =>
        Assert.Throws<InvalidOperationException>(() => Settings.HostingLocation.Default);

    /// <summary>
    /// A family whose members the host declares has no library default either.
    /// </summary>
    [Fact]
    public void Default_FamilyTheHostDeclares_Throws() =>
        Assert.Throws<InvalidOperationException>(() => Settings.HostCategoryRetention.Default);

    /// <summary>
    /// A family key is the prefix and the organization identifier or the declared
    /// category.
    /// </summary>
    [Fact]
    public void For_OrganizationIdentifier_NamesTheMembersKey() =>
        Assert.Equal(
            "stepup.enforcement.acme",
            Settings.OrganizationStepUpEnforcement.For("acme").ToString());

    /// <summary>
    /// Chapter 10 section 4 and the <c>policy.&lt;organization&gt;</c> row: the
    /// identifier a family key carries is a version 7 value, so the segment begins
    /// with a digit as often as with a letter.
    /// </summary>
    [Fact]
    public void For_OrganizationIdentifierBeginningWithADigit_NamesTheMembersKey()
    {
        var organization = new OrganizationId(new Guid("019bdf22-0000-7000-8000-000000000001"));

        Assert.Equal(
            "policy.019bdf22-0000-7000-8000-000000000001",
            Settings.OrganizationPolicy.For(organization.ToString()).ToString());
    }

    private static ErrorCode? Code<TValue>(Result<TValue> outcome) =>
        outcome.Match(_ => (ErrorCode?)null, failure => failure.Code);
}
