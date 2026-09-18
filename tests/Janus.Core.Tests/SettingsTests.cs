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
    /// A list that drops the one algorithm the chapter requires is refused.
    /// </summary>
    [Fact]
    public void Accept_DroppingTheRequiredAlgorithm_Refused()
    {
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
}
