using Janus.Authorization.Gate;
using Janus.Authorization.Model;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// What a step-up gate bound to an action asks of a caller, and what it asks of a
/// deployment that cannot say how far the caller authenticated
/// (AUTH-STEP-001, AUTH-STEP-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class StepUpGatesTests
{
    private static readonly Permission Bound = Permission.Parse("document:edit");
    private static readonly Permission Unbound = Permission.Parse("document:read");

    /// <summary>
    /// AUTH-STEP-001 AC1: the gate is bound to the permission, so exercising it asks
    /// for step-up wherever the call came from, and an unbound permission asks for
    /// none.
    /// </summary>
    [Fact]
    public void AUTH_STEP_001_AC1_ExercisingABoundPermissionAsksForStepUp()
    {
        var gates = new StepUpGates(Model(), new AssuranceProviderInMemory(AssuranceLevel.Aal1));

        Assert.Equal(ErrorCodes.StepUpRequired, gates.OutstandingOn(Bound));
        Assert.Null(gates.OutstandingOn(Unbound));
    }

    /// <summary>
    /// AUTH-STEP-001 AC2: nothing the gate reads names an application, so splitting
    /// one in two, or folding two into one, leaves every answer where it was.
    /// </summary>
    [Fact]
    public void AUTH_STEP_001_AC2_SplittingAnApplicationChangesNothing()
    {
        var one = new StepUpGates(Model(), new AssuranceProviderInMemory(AssuranceLevel.Aal1));
        var other = new StepUpGates(Model(), new AssuranceProviderInMemory(AssuranceLevel.Aal1));

        Assert.Equal(one.OutstandingOn(Bound), other.OutstandingOn(Bound));
        Assert.Equal(one.OutstandingOn(Unbound), other.OutstandingOn(Unbound));
    }

    /// <summary>
    /// AUTH-STEP-003 AC1: with no assurance provider, a permission bound to a gate is
    /// denied rather than assumed met.
    /// </summary>
    [Fact]
    public void AUTH_STEP_003_AC1_WithNoAssuranceProviderABoundPermissionIsDenied() =>
        Assert.NotNull(new StepUpGates(Model(), assurance: null).OutstandingOn(Bound));

    /// <summary>
    /// AUTH-STEP-003 AC2: the denial carries its own code, so a deployment that
    /// cannot ask for step-up is told apart from a session that has not yet stepped
    /// up.
    /// </summary>
    [Fact]
    public void AUTH_STEP_003_AC2_TheDenialIsDistinguishableFromAnOrdinaryOne()
    {
        Assert.Equal(
            ErrorCodes.StepUpUnavailable,
            new StepUpGates(Model(), assurance: null).OutstandingOn(Bound));
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            new StepUpGates(Model(), new AssuranceProviderInMemory(AssuranceLevel.Aal1)).OutstandingOn(Bound));
    }

    private static AuthorizationModel Model() => AuthorizationModel.Of(HostDomain.Declared()
        .StepUpGate(Bound.ToString(), Bound.ToString())
        .Build());
}
