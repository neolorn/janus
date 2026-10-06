using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;
using Xunit.Sdk;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// A session judged against a gate named rather than enumerated: one of chapter 10
/// section 5a, or one a host bound its own action to (AUTH-STEP-002, AUTHZ-GATE-005,
/// D-160).
/// </summary>
[Trait("kind", "unit")]
public sealed class StepUpGuardTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SessionOrigin Somewhere =
        new("198.51.100.7", new DeviceDescription("Firefox", "Fedora"));

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly SessionStoreInMemory _sessions = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly PhoneSignalAuditInMemory _considered = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly SubjectId _person;
    private PhoneSignalProvider? _provider;

    /// <summary>
    /// A person holding a password, under a system policy that raises one gate.
    /// </summary>
    public StepUpGuardTests()
    {
        _person = SubjectId.New(_randomness);
        _passwords.Hold(_person, Noon);

        var gates = Core.Policies.SystemDefault.Gates.ToDictionary();
        gates[StepUpAction.AccountSuspend] = new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5));

        _configuration.Set(Settings.PolicyDefault, Core.Policies.SystemDefault with { Gates = gates });
    }

    private StepUpGuard Guard =>
        new(
            _sessions,
            _authenticators,
            _passwords,
            new PolicyResolution(_memberships, _configuration, _raises),
            _identifiers,
            new PhoneSignals(_provider, _considered, _work, _clock),
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: where the carrier reports a recent change of SIM or of
    /// network for the account's number, the text code is withheld from the
    /// combinations a step-up offers and the account's other second steps are offered,
    /// and the consideration is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AReportedChangeWithholdsTheTextCodeFromTheCombinationsAsync()
    {
        TwoStep();
        Holds(Factor.PhoneCode);
        Holds(Factor.Totp);

        StepUpChallenge clear = await ChallengedAsync(Opened(Noon), "account:suspend");

        _provider = new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Risk));

        StepUpChallenge risky = await ChallengedAsync(Opened(Noon), "account:suspend");

        Assert.Contains(clear.Combinations, combination => combination.Contains(Factor.PhoneCode));
        Assert.Equal(StepUpOutcome.Present, risky.Outcome);
        Assert.DoesNotContain(risky.Combinations, combination => combination.Contains(Factor.PhoneCode));
        Assert.Contains(risky.Combinations, combination => combination.Contains(Factor.Totp));
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)_person)], _considered.Records);
    }

    /// <summary>
    /// AUTH-FACT-002b AC6 and AUTH-STEP-002: where the text code was the only way to the
    /// gate, withholding it leaves no combination, and the answer is the one an account
    /// that holds the level and cannot present it is given, never a pass.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AStepUpLeftWithNoCombinationIsToldToReportTheLossAsync()
    {
        TwoStep();
        Holds(Factor.PhoneCode);

        _provider = new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Risk));

        StepUpChallenge risky = await ChallengedAsync(Opened(Noon), "account:suspend");

        Assert.Equal(StepUpOutcome.ReportLoss, risky.Outcome);
        Assert.Empty(risky.Combinations);
    }

    /// <summary>
    /// AUTH-STEP-002, D-160: a gate named as in section 5a costs what the policy states
    /// for that action, so a password session just proved passes the gate the policy
    /// leaves at the account's reach and not the one it raised.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002_AGateNamedInTheCatalogueCostsItsOwnValuesAsync()
    {
        SessionId session = Opened(Noon);

        Assert.Equal(StepUpOutcome.Satisfied, (await ChallengedAsync(session, "password:set")).Outcome);
        Assert.NotEqual(StepUpOutcome.Satisfied, (await ChallengedAsync(session, "account:suspend")).Outcome);
    }

    /// <summary>
    /// AUTHZ-GATE-005, D-160: a gate the host names has no values in any policy, so it
    /// costs what the dearest gate of the person's policy costs and a session that
    /// would pass every other gate does not pass it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_005_AGateTheHostNamesCostsTheDearestGateOfThePolicyAsync()
    {
        StepUpChallenge challenge = await ChallengedAsync(Opened(Noon), "document:publish");

        Assert.NotEqual(StepUpOutcome.Satisfied, challenge.Outcome);
        Assert.Equal(AssuranceLevel.Aal2, challenge.Required);
        Assert.True(challenge.PhishingResistant);
    }

    /// <summary>
    /// AUTH-STEP-002: a session that is not the person's own proves nothing for them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002_AnotherPersonsSessionIsRefusedAsync()
    {
        Result<StepUpChallenge> refused = await Guard.ChallengeAsync(
            SubjectId.New(_randomness),
            Opened(Noon),
            "document:publish",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            refused.Match(_ => throw new XunitException("Another person's session was judged."), error => error.Code));
    }

    private async Task<StepUpChallenge> ChallengedAsync(SessionId session, string gate) =>
        (await Guard.ChallengeAsync(_person, session, gate, TestContext.Current.CancellationToken))
        .Match(challenge => challenge, error => throw new XunitException(error.Code.ToString()));

    // A gate a password and any second step reach, which is what a text code is.
    private void TwoStep()
    {
        var gates = Core.Policies.SystemDefault.Gates.ToDictionary();
        gates[StepUpAction.AccountSuspend] = new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5));

        _configuration.Set(Settings.PolicyDefault, Core.Policies.SystemDefault with { Gates = gates });

        IdentifierId number = _identifiers.Verified(_person, IdentifierKind.Phone, "+441632960011");

        _identifiers.PromoteAsync(_person, number, TestContext.Current.CancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    private void Holds(Factor factor) =>
        _authenticators.Hold(Authenticator.Existing(
            AuthenticatorId.New(_clock),
            _person,
            factor,
            CredentialLabel.TryParse(factor.ToString(), out CredentialLabel label)
                ? label
                : throw new InvalidOperationException("The catalogue entry is no label."),
            AuthenticatorState.Active,
            _clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            factor is Factor.Totp ? new TotpMaterial(new byte[20], null) : null,
            null,
            isPreferred: false));

    private SessionId Opened(DateTimeOffset at)
    {
        var session = Session.Begin(
            SessionId.New(_clock),
            _person,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Somewhere,
            at,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        _sessions.AddAsync(session, Drawn(), Drawn(), TestContext.Current.CancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return session.Id;
    }

    private byte[] Drawn()
    {
        byte[] fingerprint = new byte[32];

        _randomness.GetBytes(fingerprint);

        return fingerprint;
    }
}
