using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
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
public sealed class StepUpGuardTests : IDisposable
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
    private readonly FixedClock _clock = new(Noon);
    private readonly SubjectId _person;

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
            _clock);

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

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
            satisfiesEveryGate: false);

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
