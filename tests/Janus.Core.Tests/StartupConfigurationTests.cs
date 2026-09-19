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
    public void LIB_HOST_001_AC1_NamingOnlyTheDeclarationsStarts() => Start(Named());

    /// <summary>
    /// LIB-HOST-001 AC2: a missing declaration stops startup, and the fault names the
    /// key the operator has to supply.
    /// </summary>
    [Fact]
    public void LIB_HOST_001_AC2_AMissingDeclarationNamesTheKey()
    {
        StartupException fault = Assert.Throws<StartupException>(
            () => Start(Named(without: Settings.AlertingOwnerSms.Key)));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, fault.Failure?.Code);
        Assert.Equal(
            "alerting.owner.sms",
            fault.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// LIB-HOST-001 AC4: the governing language has a code of its own, so a host that
    /// omits it is told which condition stopped startup rather than only which key.
    /// </summary>
    [Fact]
    public void LIB_HOST_001_AC4_TheGoverningLanguageFailsWithItsNamedError()
    {
        StartupException fault = Assert.Throws<StartupException>(
            () => Start(Named(without: Settings.LegalGoverningLanguage.Key)));

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
            () => Start(
                Named(without: Settings.HostingCrossBorderBasis.Key),
                HostingLocation.Outside));

        Assert.Equal(
            "hosting.crossborderbasis",
            fault.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// INT-HOST-001 AC2: hosting inside Egypt is not a transfer, so the same
    /// deployment starts without a basis.
    /// </summary>
    [Fact]
    public void INT_HOST_001_AC2_HostingInsideEgyptNeedsNoCrossBorderBasis() =>
        Start(Named(without: Settings.HostingCrossBorderBasis.Key));

    /// <summary>
    /// AUTH-PASS-004: the context source forbids the service's own name in a
    /// password, so it is the deployment that says what that name is.
    /// </summary>
    [Fact]
    public void ThrowIfIncomplete_TheContextSourceIsOn_RequiresTheServiceName()
    {
        StartupException fault = Assert.Throws<StartupException>(
            () => Start(
                Named(without: Settings.ServiceName.Key),
                blocklistSources: new HashSet<BlocklistRejectionSource>
                {
                    BlocklistRejectionSource.Leaked,
                    BlocklistRejectionSource.Context,
                }));

        Assert.Equal("service.name", fault.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// The same deployment with the source off starts without naming the service.
    /// </summary>
    [Fact]
    public void ThrowIfIncomplete_TheContextSourceIsOff_NeedsNoServiceName() =>
        Start(Named(without: Settings.ServiceName.Key));

    /// <summary>
    /// PRIV-ROPA-001: the hosting environment is a cell of the register, so it is
    /// needed only by a deployment that generates one.
    /// </summary>
    [Fact]
    public void ThrowIfIncomplete_TheRegisterIsGenerated_RequiresTheHostingEnvironment()
    {
        StartupException fault = Assert.Throws<StartupException>(
            () => Start(
                Named(without: Settings.HostingEnvironment.Key),
                recordsOfProcessing: true));

        Assert.Equal("hosting.environment", fault.Failure?.Details["key"].GetString());
    }

    /// <summary>
    /// A deployment that generates no register starts without it.
    /// </summary>
    [Fact]
    public void ThrowIfIncomplete_NoRegisterIsGenerated_NeedsNoHostingEnvironment() =>
        Start(Named(without: Settings.HostingEnvironment.Key));

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

    // Startup against a deployment inside Egypt that screens passwords against the
    // leaked list alone and generates no register, which is the shipped shape.
    private static void Start(
        IReadOnlySet<ConfigurationKey> named,
        HostingLocation location = HostingLocation.Inside,
        IReadOnlySet<BlocklistRejectionSource>? blocklistSources = null,
        bool recordsOfProcessing = false) =>
        Settings.ThrowIfIncomplete(
            named,
            location,
            blocklistSources ?? Settings.PasswordBlocklistSources.Default,
            recordsOfProcessing);

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
