using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Authorization.Model;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// What a step-up gate bound to an action asks of a caller, and what it asks of a
/// deployment that cannot say how far the caller authenticated
/// (AUTH-STEP-001, AUTH-STEP-002, AUTH-STEP-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class StepUpGatesTests
{
    private const string Gate = "article:publish";

    private static readonly Permission Bound = Permission.Parse("article:edit");
    private static readonly Permission Unbound = Permission.Parse("article:read");

    private readonly SubjectId _holder = Identifiers.Subject();

    /// <summary>
    /// AUTH-STEP-001 AC1: the gate is bound to the permission, so exercising it asks
    /// for step-up wherever the call came from, and an unbound permission asks for
    /// none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_001_AC1_ExercisingABoundPermissionAsksForStepUpAsync()
    {
        var gates = new StepUpGates(Model(), sessions: null, new AssuranceProviderInMemory(AssuranceLevel.Aal1));

        Assert.Equal(ErrorCodes.StepUpRequired, await OutstandingAsync(gates, Bound));
        Assert.Null(await OutstandingAsync(gates, Unbound));
    }

    /// <summary>
    /// AUTH-STEP-001 AC2: nothing the gate reads names an application, so splitting
    /// one in two, or folding two into one, leaves every answer where it was.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_001_AC2_SplittingAnApplicationChangesNothingAsync()
    {
        var one = new StepUpGates(Model(), sessions: null, new AssuranceProviderInMemory(AssuranceLevel.Aal1));
        var other = new StepUpGates(Model(), sessions: null, new AssuranceProviderInMemory(AssuranceLevel.Aal1));

        Assert.Equal(await OutstandingAsync(one, Bound), await OutstandingAsync(other, Bound));
        Assert.Equal(await OutstandingAsync(one, Unbound), await OutstandingAsync(other, Unbound));
    }

    /// <summary>
    /// AUTH-STEP-002 AC3, AUTHZ-GATE-005 (D-160): where the acting person's own
    /// session carries the request, the gate bound to a host's action is judged against
    /// it, so a session that met the gate is not challenged and one that did not is
    /// refused naming the gate.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002_AC3_ASessionThatMeetsAHostsGateIsNotChallengedAsync()
    {
        var sessions = new SessionGatesInMemory(_holder);
        var gates = new StepUpGates(Model(), sessions, assurance: null);

        Error refused = Assert.IsType<Error>(await gates.OutstandingAsync(
            AccessContext.Of(_holder),
            Bound,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Equal(Gate, refused.Details["action"].GetString());

        sessions.Meets(Gate);

        Assert.Null(await OutstandingAsync(gates, Bound));
        Assert.Equal([Gate, Gate], sessions.Asked);
    }

    /// <summary>
    /// AUTH-STEP-002, AUTH-STEP-003: a context the request's session does not belong
    /// to is not judged by that session, so the session proves nothing for it and the
    /// gate stays unmet.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002_AnotherPersonsSessionMeetsNoGateAsync()
    {
        var sessions = new SessionGatesInMemory(Identifiers.Subject());
        sessions.Meets(Gate);

        var gates = new StepUpGates(Model(), sessions, assurance: null);

        Assert.Equal(ErrorCodes.StepUpUnavailable, await OutstandingAsync(gates, Bound));
        Assert.Empty(sessions.Asked);
    }

    /// <summary>
    /// AUTH-STEP-003 AC1: with no assurance provider, a permission bound to a gate is
    /// denied rather than assumed met.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_003_AC1_WithNoAssuranceProviderABoundPermissionIsDeniedAsync() =>
        Assert.NotNull(await OutstandingAsync(new StepUpGates(Model(), sessions: null, assurance: null), Bound));

    /// <summary>
    /// AUTH-STEP-003 AC2: the denial carries its own code, so a deployment that
    /// cannot ask for step-up is told apart from a session that has not yet stepped
    /// up.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_003_AC2_TheDenialIsDistinguishableFromAnOrdinaryOneAsync()
    {
        Assert.Equal(
            ErrorCodes.StepUpUnavailable,
            await OutstandingAsync(new StepUpGates(Model(), sessions: null, assurance: null), Bound));
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            await OutstandingAsync(
                new StepUpGates(Model(), sessions: null, new AssuranceProviderInMemory(AssuranceLevel.Aal1)),
                Bound));
    }

    private static AuthorizationModel Model() => AuthorizationModel.Of(HostDomain.Declared()
        .StepUpGate(Bound.ToString(), Gate)
        .Build());

    private async Task<ErrorCode?> OutstandingAsync(StepUpGates gates, Permission permission) =>
        (await gates.OutstandingAsync(AccessContext.Of(_holder), permission, TestContext.Current.CancellationToken))?.Code;
}
