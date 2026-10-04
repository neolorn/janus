using System;
using System.Text.Json;
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
        var gates = new StepUpGates(sessions: null, new AssuranceProviderInMemory(Reported(AssuranceLevel.Aal1)), TimeProvider.System);

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
        var one = new StepUpGates(sessions: null, new AssuranceProviderInMemory(Reported(AssuranceLevel.Aal1)), TimeProvider.System);
        var other = new StepUpGates(sessions: null, new AssuranceProviderInMemory(Reported(AssuranceLevel.Aal1)), TimeProvider.System);

        Assert.Equal(await OutstandingAsync(one, Bound), await OutstandingAsync(other, Bound));
        Assert.Equal(await OutstandingAsync(one, Unbound), await OutstandingAsync(other, Unbound));
    }

    /// <summary>
    /// AUTH-STEP-002 AC3, AUTHZ-GATE-005 (D-160): where the acting person's own
    /// session carries the request, the gate bound to a host's action is judged against
    /// it, so a session that met the gate is not challenged and one that did not is
    /// refused, the session having been asked about that gate.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002_AC3_ASessionThatMeetsAHostsGateIsNotChallengedAsync()
    {
        var sessions = new SessionGatesInMemory(_holder);
        var gates = new StepUpGates(sessions, assurance: null, TimeProvider.System);

        Error refused = Assert.IsType<Error>(await gates.OutstandingAsync(
            AccessContext.Of(_holder),
            Model().GateOf(Bound),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Equal([Gate], sessions.Asked);

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

        var gates = new StepUpGates(sessions, assurance: null, TimeProvider.System);

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
        Assert.NotNull(await OutstandingAsync(new StepUpGates(sessions: null, assurance: null, TimeProvider.System), Bound));

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
            await OutstandingAsync(new StepUpGates(sessions: null, assurance: null, TimeProvider.System), Bound));
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            await OutstandingAsync(
                new StepUpGates(sessions: null, new AssuranceProviderInMemory(Reported(AssuranceLevel.Aal1)), TimeProvider.System),
                Bound));
    }

    /// <summary>
    /// LIB-HOST-004 AC3: where no session of the library carries the request, the gate
    /// costs what the acting person's policy says, and a provider reporting a proof that
    /// reaches the level, recently enough, admits the action; a gate asking for what the
    /// account can reach reads the reachable level the provider reports.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_004_AProviderReportingTheGateMetAdmitsTheActionAsync()
    {
        var sessions = new SessionGatesInMemory(Identifiers.Subject());

        sessions.Costs(Gate, new Core.Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5)));

        Assert.Null(await OutstandingAsync(Reporting(sessions, Reported(AssuranceLevel.Aal2)), Bound));

        sessions.Costs(Gate, new Core.Gate(GateLevel.Reachable, PhishingResistant: false, TimeSpan.FromMinutes(5)));

        Assert.Null(await OutstandingAsync(Reporting(sessions, Reported(AssuranceLevel.Aal1)), Bound));
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            await OutstandingAsync(
                Reporting(sessions, Reported(AssuranceLevel.Aal1) with { Reachable = AssuranceLevel.Aal2 }),
                Bound));
        Assert.Empty(sessions.Asked);
    }

    /// <summary>
    /// LIB-HOST-004 AC3: a proof older than the gate's maximum age meets nothing, and the
    /// refusal carries the gate, the outcome <c>present</c>, no options and no pending
    /// instant; a report that cannot be read is refused the same way.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_004_AProviderReportingAnOlderProofIsRefusedWithTheGateAsync()
    {
        var sessions = new SessionGatesInMemory(Identifiers.Subject());

        sessions.Costs(Gate, new Core.Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5)));

        Error refused = Assert.IsType<Error>(await RefusalAsync(Reporting(
            sessions,
            Reported(AssuranceLevel.Aal2) with { AttainedAt = TimeProvider.System.GetUtcNow() - TimeSpan.FromMinutes(10) })));

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Equal(
            "{\"level\":\"aal2\",\"phishingResistant\":false,\"maxAge\":300}",
            refused.Details["required"].GetRawText());
        Assert.Equal("present", refused.Details["outcome"].GetString());
        Assert.Equal(0, refused.Details["options"].GetArrayLength());
        Assert.Equal(JsonValueKind.Null, refused.Details["pendingUntil"].ValueKind);
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            await OutstandingAsync(Reporting(sessions, attained: null), Bound));
    }

    /// <summary>
    /// LIB-HOST-004 AC3: a gate asking for phishing resistance is not met by a proof the
    /// provider reports was not phishing-resistant, whatever level it reached.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_004_AProviderReportingNoPhishingResistanceMeetsNoPhishingResistantGateAsync()
    {
        var sessions = new SessionGatesInMemory(Identifiers.Subject());

        sessions.Costs(Gate, new Core.Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5)));

        Error refused = Assert.IsType<Error>(await RefusalAsync(Reporting(sessions, Reported(AssuranceLevel.Aal2))));

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.True(refused.Details["required"].GetProperty("phishingResistant").GetBoolean());
        Assert.Null(await OutstandingAsync(
            Reporting(sessions, Reported(AssuranceLevel.Aal2) with { PhishingResistant = true }),
            Bound));
    }

    /// <summary>
    /// LIB-HOST-004 AC4: a report whose instant is after now, whose level or reachable
    /// assurance is not a level of chapter 10 section 5.4, or that the provider fails to
    /// give, meets no gate and is refused with the gate, the outcome <c>present</c> and
    /// no options.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_004_AC4_AReportThatDoesNotReadMeetsNoGateAsync()
    {
        var sessions = new SessionGatesInMemory(Identifiers.Subject());

        sessions.Costs(Gate, new Core.Gate(GateLevel.Reachable, PhishingResistant: false, TimeSpan.FromMinutes(5)));

        AttainedAssurance read = Reported(AssuranceLevel.Aal2);
        AttainedAssurance?[] unread =
        [
            read with { AttainedAt = TimeProvider.System.GetUtcNow() + TimeSpan.FromMinutes(1) },
            read with { Level = (AssuranceLevel)4 },
            read with { Reachable = (AssuranceLevel)(-1) },
            null,
        ];

        Assert.Null(await RefusalAsync(Reporting(sessions, read)));

        foreach (AttainedAssurance? report in unread)
        {
            Error refused = Assert.IsType<Error>(await RefusalAsync(Reporting(sessions, report)));

            Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
            Assert.Equal(
                "{\"level\":\"aal1\",\"phishingResistant\":false,\"maxAge\":300}",
                refused.Details["required"].GetRawText());
            Assert.Equal("present", refused.Details["outcome"].GetString());
            Assert.Equal(0, refused.Details["options"].GetArrayLength());
            Assert.Equal(JsonValueKind.Null, refused.Details["pendingUntil"].ValueKind);
        }
    }

    /// <summary>
    /// LIB-HOST-004 AC4: where the acting person's own session of the library carries
    /// the request, that session is judged and the provider is not asked.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_004_AC4_TheActingPersonsOwnSessionIsJudgedInPlaceOfTheProviderAsync()
    {
        var sessions = new SessionGatesInMemory(_holder);
        var provider = new AssuranceProviderInMemory(Reported(AssuranceLevel.Aal2));
        var gates = new StepUpGates(sessions, provider, TimeProvider.System);

        Assert.Equal(ErrorCodes.StepUpRequired, await OutstandingAsync(gates, Bound));

        sessions.Meets(Gate);

        Assert.Null(await OutstandingAsync(gates, Bound));
        Assert.Equal([Gate, Gate], sessions.Asked);
        Assert.Equal(0, provider.Asked);
    }

    // What a host reports of a proof made a minute ago, reaching no further than it.
    private static AttainedAssurance Reported(AssuranceLevel level) =>
        new(level, PhishingResistant: false, TimeProvider.System.GetUtcNow() - TimeSpan.FromMinutes(1), level);

    private static StepUpGates Reporting(SessionGatesInMemory sessions, AttainedAssurance? attained) =>
        new(sessions, new AssuranceProviderInMemory(attained), TimeProvider.System);

    private static AuthorizationModel Model() => AuthorizationModel.Of(HostDomain.Declared()
        .StepUpGate(Bound.ToString(), Gate)
        .Build());

    private async Task<Error?> RefusalAsync(StepUpGates gates) =>
        await gates.OutstandingAsync(
            AccessContext.Of(_holder),
            Model().GateOf(Bound),
            TestContext.Current.CancellationToken);

    // The gate the host bound the permission to, as the access gate reads it.
    private async Task<ErrorCode?> OutstandingAsync(StepUpGates gates, Permission permission) =>
        (await gates.OutstandingAsync(
            AccessContext.Of(_holder),
            Model().GateOf(permission),
            TestContext.Current.CancellationToken))?.Code;
}
