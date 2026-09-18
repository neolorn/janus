using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What the library does with a deployment's configuration before it starts: it
/// refuses to start where a value it cannot guess is missing (LIB-HOST-001), it
/// refuses a value outside what the key admits rather than clamping it (OPS-CFG-003),
/// and it holds the two rules that span a pair of keys.
/// </summary>
[Trait("kind", "unit")]
public sealed class StartupConfigurationTests
{
    /// <summary>
    /// LIB-HOST-001 AC1: a deployment that names the declarations and nothing else
    /// starts, because every other key carries a default.
    /// </summary>
    [Fact]
    public void LIB_HOST_001_AC1_NamingOnlyTheDeclarationsStarts() =>
        Settings.ThrowIfIncomplete(Named(), HostingLocation.Inside);

    /// <summary>
    /// LIB-HOST-001 AC2: a missing declaration stops startup, and the fault names the
    /// key the operator has to supply.
    /// </summary>
    [Fact]
    public void LIB_HOST_001_AC2_AMissingDeclarationNamesTheKey()
    {
        StartupException fault = Assert.Throws<StartupException>(
            () => Settings.ThrowIfIncomplete(
                Named(without: Settings.AlertingOwnerSms.Key),
                HostingLocation.Inside));

        Assert.Contains("alerting.owner.sms", fault.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// LIB-HOST-001 AC4: the governing language has a code of its own, so a host that
    /// omits it is told which condition stopped startup rather than only which key.
    /// </summary>
    [Fact]
    public void LIB_HOST_001_AC4_TheGoverningLanguageFailsWithItsNamedError()
    {
        StartupException fault = Assert.Throws<StartupException>(
            () => Settings.ThrowIfIncomplete(
                Named(without: Settings.LegalGoverningLanguage.Key),
                HostingLocation.Inside));

        Assert.Equal(ErrorCodes.StartupGoverningLanguage, fault.Failure?.Code);
    }

    /// <summary>
    /// INT-HOST-001 AC2: hosting outside Egypt makes the cross-border basis a value
    /// the deployment has to state.
    /// </summary>
    [Fact]
    public void INT_HOST_001_AC2_HostingOutsideEgyptRequiresTheCrossBorderBasis()
    {
        StartupException fault = Assert.Throws<StartupException>(
            () => Settings.ThrowIfIncomplete(
                Named(without: Settings.HostingCrossBorderBasis.Key),
                HostingLocation.Outside));

        Assert.Contains("hosting.crossborderbasis", fault.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// INT-HOST-001 AC2: hosting inside Egypt is not a transfer, so the same
    /// deployment starts without a basis.
    /// </summary>
    [Fact]
    public void INT_HOST_001_AC2_HostingInsideEgyptNeedsNoCrossBorderBasis() =>
        Settings.ThrowIfIncomplete(
            Named(without: Settings.HostingCrossBorderBasis.Key),
            HostingLocation.Inside);

    /// <summary>
    /// OPS-CFG-003 AC3: a value outside the key's bounds stops startup rather than
    /// being clamped into range behind the operator's back.
    /// </summary>
    [Fact]
    public void OPS_CFG_003_AC3_AValueOutsideItsBoundsStopsStartup()
    {
        StartupException fault = Assert.Throws<StartupException>(
            () => Settings.PasswordFloorSingleFactor.AcceptAtStartup(14));

        Assert.Equal(ErrorCodes.ConfigurationValueBelowFloor, fault.Failure?.Code);
    }

    /// <summary>
    /// A value inside the bounds is handed back unchanged.
    /// </summary>
    [Fact]
    public void AcceptAtStartup_InsideTheBounds_ReturnsTheValue() =>
        Assert.Equal(20, Settings.PasswordFloorSingleFactor.AcceptAtStartup(20));

    /// <summary>
    /// Chapter 10 section 4.2: the floor that applies with a second factor may not
    /// exceed the floor that applies without one.
    /// </summary>
    [Fact]
    public void AcceptPasswordFloorPair_WithMfaAboveSingleFactor_Refused()
    {
        Result outcome = Settings.AcceptPasswordFloorPair(singleFactor: 15, withMfa: 16);

        Assert.Equal(ErrorCodes.ConfigurationValueAboveCeiling, Code(outcome));
    }

    /// <summary>
    /// The two floors may be equal: the rule is that one does not exceed the other.
    /// </summary>
    [Fact]
    public void AcceptPasswordFloorPair_EqualFloors_Accepted() =>
        Assert.Null(Code(Settings.AcceptPasswordFloorPair(singleFactor: 15, withMfa: 15)));

    /// <summary>
    /// The shipped defaults satisfy the rule between the two floors.
    /// </summary>
    [Fact]
    public void AcceptPasswordFloorPair_TheShippedDefaults_Accepted() =>
        Assert.Null(Code(Settings.AcceptPasswordFloorPair(
            Settings.PasswordFloorSingleFactor.Default,
            Settings.PasswordFloorWithMfa.Default)));

    /// <summary>
    /// AUTH-PASS-007: the Argon2id floor is a rule over the memory and the iterations
    /// together, so each shipped strength class is admissible on its own.
    /// </summary>
    [Theory]
    [InlineData(19456, 2)]
    [InlineData(12288, 3)]
    [InlineData(9216, 4)]
    [InlineData(7168, 5)]
    public void AcceptArgon2Cost_AStrengthClass_Accepted(int memory, int iterations) =>
        Assert.Null(Code(Settings.AcceptArgon2Cost(memory, iterations)));

    /// <summary>
    /// A pair under every strength class is refused, and the failure names the classes
    /// rather than raising the pair to the nearest one.
    /// </summary>
    [Fact]
    public void AcceptArgon2Cost_BelowEveryStrengthClass_Refused()
    {
        Result outcome = Settings.AcceptArgon2Cost(memory: 7168, iterations: 4);

        Assert.Equal(ErrorCodes.ConfigurationValueBelowFloor, Code(outcome));
    }

    /// <summary>
    /// AUTH-PASS-007: memory above a class with that class's iterations is stronger
    /// than the class, so it is admissible.
    /// </summary>
    [Fact]
    public void AcceptArgon2Cost_AboveAStrengthClass_Accepted() =>
        Assert.Null(Code(Settings.AcceptArgon2Cost(memory: 65536, iterations: 3)));

    /// <summary>
    /// The shipped defaults satisfy the Argon2id floor.
    /// </summary>
    [Fact]
    public void AcceptArgon2Cost_TheShippedDefaults_Accepted() =>
        Assert.Null(Code(Settings.AcceptArgon2Cost(
            Settings.PasswordArgon2Memory.Default,
            Settings.PasswordArgon2Iterations.Default)));

    // The keys a deployment names, less one where a test is about omitting it.
    private static HashSet<ConfigurationKey> Named(ConfigurationKey? without = null) =>
        Settings.Required
            .Select(setting => setting.Key)
            .Where(key => without is not { } omitted || key != omitted)
            .ToHashSet();

    private static ErrorCode? Code(Result outcome)
    {
        ErrorCode? code = null;

        outcome.Switch(() => { }, failure => code = failure.Code);

        return code;
    }
}
