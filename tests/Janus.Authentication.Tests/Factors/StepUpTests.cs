using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// What a step-up gate asks of a session, what the account may present to reach it,
/// and what it is told when it can present nothing (AUTH-STEP-002, AUTH-STEP-004 to
/// AUTH-STEP-008).
/// </summary>
[Trait("kind", "unit")]
public sealed class StepUpTests : IDisposable
{
    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Recency = TimeSpan.FromMinutes(15);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    /// <summary>
    /// AUTH-STEP-006 AC1: password only reaches AAL1; password and a code generator
    /// reach AAL2 without resisting relay; a passkey reaches AAL2 resisting it; an
    /// account holding only a social credential reaches nothing of ours.
    /// </summary>
    [Fact]
    public void AUTH_STEP_006_AC1_WhatAnAccountReachesIsWhatItsFactorsReach()
    {
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            StepUp.Reachable(Set(Factor.Password)));
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: false),
            StepUp.Reachable(Set(Factor.Password, Factor.Totp)));
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: false),
            StepUp.Reachable(Set(Factor.Password, Factor.PhoneCode)));
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            StepUp.Reachable(Set(Factor.Passkey)));
        Assert.Equal(
            new Assurance(AssuranceLevel.Delegated, PhishingResistant: false),
            StepUp.Reachable(Set(Factor.Google)));
    }

    /// <summary>
    /// AUTH-STEP-006 AC3: a set of single-use codes stands in for a second factor at
    /// a gate and does not make the account an account that reaches two factors.
    /// </summary>
    [Fact]
    public void AUTH_STEP_006_AC3_RecoveryCodesChangeNothingAboutWhatIsReachable() =>
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            StepUp.Reachable(Set(Factor.Password, Factor.RecoveryCodes)));

    /// <summary>
    /// AUTH-STEP-006: an email factor and a sign-in link contribute nothing to what
    /// an account reaches, whichever channel carries them.
    /// </summary>
    [Fact]
    public void AUTH_STEP_006_AnEmailFactorReachesNothing() =>
        Assert.Equal(
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            StepUp.Reachable(Set(Factor.Password, Factor.EmailCode, Factor.PhoneLink)));

    /// <summary>
    /// AUTH-STEP-006 AC2: a reported loss lowers nothing until its window completes,
    /// the factor standing against the account until it is invalidated.
    /// </summary>
    [Fact]
    public void AUTH_STEP_006_AC2_ASuspendedFactorStillStands()
    {
        var suspended = HeldFactors.Of([Suspended(Factor.Totp)], password: true);
        var invalidated = HeldFactors.Of([Invalidated(Factor.Totp)], password: true);

        Assert.Equal(AssuranceLevel.Aal2, StepUp.Reachable(suspended.Standing).Level);
        Assert.Equal(AssuranceLevel.Aal1, StepUp.Reachable(invalidated.Standing).Level);
        Assert.DoesNotContain(Factor.Totp, suspended.Usable);
    }

    /// <summary>
    /// AUTH-STEP-002 AC3: a session that proved what the gate asks, recently enough,
    /// is not challenged.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC3_ASessionThatAlreadyProvedItIsNotChallenged() =>
        Assert.Equal(
            StepUpOutcome.Satisfied,
            StepUp.On(
                    Signed(new Assurance(AssuranceLevel.Aal2, PhishingResistant: true)),
                    Gate(GateLevel.Aal2, phishingResistant: true),
                    Held(password: true, Factor.Passkey),
                    Noon + TimeSpan.FromMinutes(14))
                .Outcome);

    /// <summary>
    /// AUTH-STEP-002 AC3: evidence older than the gate's maximum age proves nothing.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC3_EvidenceOlderThanTheMaximumAgeIsNotEnough() =>
        Assert.Equal(
            StepUpOutcome.Present,
            StepUp.On(
                    Signed(new Assurance(AssuranceLevel.Aal2, PhishingResistant: true)),
                    Gate(GateLevel.Aal2, phishingResistant: true),
                    Held(password: true, Factor.Passkey),
                    Noon + TimeSpan.FromMinutes(16))
                .Outcome);

    /// <summary>
    /// AUTH-STEP-002 AC4, AC6: on an account reaching two factors, a bare password
    /// passes no gate, and every combination that reaches it is offered.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC4_EveryCombinationThatReachesTheGateIsOffered()
    {
        StepUpChallenge challenge = Challenge(
            Gate(GateLevel.Aal2, phishingResistant: false),
            Held(password: true, Factor.Totp, Factor.RecoveryCodes, Factor.SecurityKey));

        Assert.Equal(StepUpOutcome.Present, challenge.Outcome);
        Assert.Equal(
            [
                [Factor.Password, Factor.Totp],
                [Factor.Password, Factor.SecurityKey],
                [Factor.Password, Factor.RecoveryCodes],
            ],
            Offered(challenge));
    }

    /// <summary>
    /// AUTH-STEP-002 AC4a: password and an SMS code pass a gate declared two factors
    /// and are not offered where relay resistance is required.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC4a_AnSmsCodePassesOnlyWhereRelayResistanceIsNotAsked()
    {
        HeldFactors held = Held(password: true, Factor.PhoneCode, Factor.SecurityKey);

        Assert.Contains(
            new List<Factor> { Factor.Password, Factor.PhoneCode },
            Offered(Challenge(Gate(GateLevel.Aal2, phishingResistant: false), held)));
        Assert.DoesNotContain(
            new List<Factor> { Factor.Password, Factor.PhoneCode },
            Offered(Challenge(Gate(GateLevel.Aal2, phishingResistant: true), held)));
    }

    /// <summary>
    /// AUTH-STEP-002 AC4b, AC6: a passkey alone and a password beside a security key
    /// pass a gate that requires relay resistance; a code generator and a recovery
    /// code beside a password do not. Every combination that reaches it is offered,
    /// including one that carries more than it needs to.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC4b_OnlyRelayResistantCombinationsPassSuchAGate() =>
        Assert.Equal(
            [
                [Factor.Passkey],
                [Factor.Password, Factor.SecurityKey],
                [Factor.Passkey, Factor.Totp],
                [Factor.Passkey, Factor.SecurityKey],
                [Factor.Passkey, Factor.RecoveryCodes],
            ],
            Offered(Challenge(
                Gate(GateLevel.Aal2, phishingResistant: true),
                Held(
                    password: true,
                    Factor.Passkey,
                    Factor.SecurityKey,
                    Factor.Totp,
                    Factor.RecoveryCodes))));

    /// <summary>
    /// AUTH-STEP-002 AC5: a customer holding only a password passes a gate asking
    /// for what the account can reach, with the password.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC5_APasswordOnlyAccountPassesItsOwnGate()
    {
        StepUpChallenge challenge = Challenge(
            Gate(GateLevel.Reachable, phishingResistant: false),
            Held(password: true));

        Assert.Equal(StepUpOutcome.Present, challenge.Outcome);
        Assert.Equal(AssuranceLevel.Aal1, challenge.Required);
        Assert.Equal([[Factor.Password]], Offered(challenge));
    }

    /// <summary>
    /// AUTH-STEP-002a: a gate asking for what the account can reach asks for the most
    /// it can do and never more.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002a_AReachableGateAsksForWhatTheAccountCanDo() =>
        Assert.Equal(
            AssuranceLevel.Aal2,
            Challenge(
                    Gate(GateLevel.Reachable, phishingResistant: false),
                    Held(password: true, Factor.Totp))
                .Required);

    /// <summary>
    /// AUTH-STEP-005 AC1, AC2: a social credential is never offered, and a subject
    /// holding only one is told to enrol.
    /// </summary>
    [Fact]
    public void AUTH_STEP_005_AC1_ASocialCredentialSatisfiesNoGate()
    {
        StepUpChallenge challenge = Challenge(
            Gate(GateLevel.Reachable, phishingResistant: false),
            Held(password: false, Factor.Google));

        Assert.Equal(StepUpOutcome.Enrol, challenge.Outcome);
        Assert.Equal(AssuranceLevel.Aal1, challenge.Required);
        Assert.Empty(challenge.Combinations);
    }

    /// <summary>
    /// AUTH-STEP-002 AC7: an account that has never held what the gate asks is told
    /// to enrol.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC7_AnAccountBelowTheGateIsToldToEnrol() =>
        Assert.Equal(
            StepUpOutcome.Enrol,
            Challenge(Gate(GateLevel.Aal2, phishingResistant: true), Held(password: true))
                .Outcome);

    /// <summary>
    /// AUTH-STEP-002 AC7: an account that reaches the gate but cannot present what
    /// would satisfy it is told to report the loss.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC7_AnAccountThatCannotPresentItIsToldToReportALoss()
    {
        StepUpChallenge challenge = StepUp.On(
            Signed(new Assurance(AssuranceLevel.Aal1, PhishingResistant: false)),
            Gate(GateLevel.Reachable, phishingResistant: false),
            new HeldFactors(Set(Factor.Password), Set(Factor.Password, Factor.Totp), null),
            Noon);

        Assert.Equal(StepUpOutcome.ReportLoss, challenge.Outcome);
        Assert.Equal(AssuranceLevel.Aal2, challenge.Required);
    }

    /// <summary>
    /// AUTH-STEP-002 AC7: a loss report already pending is said so, with the instant
    /// it completes, and the gate stays closed until then.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC7_APendingLossReportIsShownWithItsCompletion()
    {
        DateTimeOffset completes = Noon + TimeSpan.FromDays(7);

        StepUpChallenge challenge = StepUp.On(
            Signed(new Assurance(AssuranceLevel.Aal1, PhishingResistant: false)),
            Gate(GateLevel.Reachable, phishingResistant: false),
            HeldFactors.Of([Suspended(Factor.Totp, completes)], password: true),
            Noon);

        Assert.Equal(StepUpOutcome.LossPending, challenge.Outcome);
        Assert.Equal(completes, challenge.LossCompletes);
    }

    /// <summary>
    /// AUTH-STEP-004 AC1, AC3: a break-glass session satisfies every gate, and only
    /// for as long as it stands.
    /// </summary>
    [Fact]
    public void AUTH_STEP_004_AC1_ABreakGlassSessionSatisfiesEveryGate()
    {
        var emergency = Session.Begin(
            SessionId.New(TimeProvider.System),
            SubjectId.New(_randomness),
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Origin(),
            Noon,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1),
            satisfiesEveryGate: true);

        Assert.Equal(
            StepUpOutcome.Satisfied,
            StepUp.On(
                    emergency,
                    Gate(GateLevel.Aal2, phishingResistant: true),
                    Held(password: true),
                    Noon + TimeSpan.FromMinutes(59))
                .Outcome);
    }

    /// <summary>
    /// AUTH-STEP-007 AC2: a password-only account enrols a passkey after presenting
    /// the password, the lower of what it reaches and what the passkey contributes.
    /// </summary>
    [Fact]
    public void AUTH_STEP_007_AC2_EnrolmentIsGatedAtTheLowerOfTheTwo()
    {
        StepUpChallenge challenge = StepUp.ToEnrol(
            Signed(null),
            Gate(GateLevel.Aal2, phishingResistant: true),
            Held(password: true),
            Factor.Passkey,
            Noon);

        Assert.Equal(StepUpOutcome.Present, challenge.Outcome);
        Assert.Equal(AssuranceLevel.Aal1, challenge.Required);
        Assert.False(challenge.PhishingResistant);
        Assert.Equal([[Factor.Password]], Offered(challenge));
    }

    /// <summary>
    /// AUTH-STEP-007 AC2: an account that reaches two factors presents two to enrol
    /// another credential.
    /// </summary>
    [Fact]
    public void AUTH_STEP_007_AC2_AnAccountReachingTwoFactorsPresentsTwo()
    {
        StepUpChallenge challenge = StepUp.ToEnrol(
            Signed(null),
            Gate(GateLevel.Aal2, phishingResistant: true),
            Held(password: true, Factor.Totp),
            Factor.Passkey,
            Noon);

        Assert.Equal(AssuranceLevel.Aal2, challenge.Required);
        Assert.Equal([[Factor.Password, Factor.Totp]], Offered(challenge));
    }

    /// <summary>
    /// AUTH-STEP-007: an account holding only a social credential sets a password
    /// with no further gate, delegated being the lower value.
    /// </summary>
    [Fact]
    public void AUTH_STEP_007_ASocialOnlyAccountSetsAPasswordWithNoFurtherGate() =>
        Assert.Equal(
            StepUpOutcome.Satisfied,
            StepUp.ToEnrol(
                    Signed(new Assurance(AssuranceLevel.Delegated, PhishingResistant: false)),
                    Gate(GateLevel.Reachable, phishingResistant: false),
                    Held(password: false, Factor.Google),
                    Factor.Password,
                    Noon)
                .Outcome);

    /// <summary>
    /// AUTH-STEP-008 invariant 3: from every account state and for every gate, the
    /// answer is a move and never a bare refusal, and an offer never comes empty.
    /// </summary>
    [Fact]
    public void AUTH_STEP_008_EveryAccountStateAndGateHasAMove()
    {
        foreach (HeldFactors held in Accounts())
        {
            foreach (Gate gate in Gates())
            {
                StepUpChallenge challenge = Challenge(gate, held);

                Assert.True(
                    challenge.Outcome is StepUpOutcome.Present
                        ? challenge.Combinations.Count > 0
                        : challenge.Combinations.Count == 0,
                    "A gate answered " + challenge.Outcome + " with "
                    + challenge.Combinations.Count + " combinations.");
            }
        }
    }

    /// <summary>
    /// AUTH-STEP-002 AC8, AUTH-STEP-008 invariant 2: a gate is decided from the
    /// session record and what the account reaches, so two accounts reaching the same
    /// thing are asked the same thing whatever they hold.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC8_TwoAccountsReachingTheSameAreAskedTheSame()
    {
        Gate gate = Gate(GateLevel.Reachable, phishingResistant: false);

        Assert.Equal(
            Challenge(gate, Held(password: true, Factor.Totp)).Required,
            Challenge(gate, Held(password: true, Factor.PhoneCode)).Required);
    }

    /// <summary>
    /// AUTH-STEP-001 AC1, AC3: what an action costs is the principal's policy's gate
    /// and nothing about where the request came from, so the same action on a second
    /// organization's policy asks for what that organization set and leaves the
    /// first where it stood.
    /// </summary>
    [Fact]
    public void AUTH_STEP_001_AC3_ASecondOrganizationSetsItsOwnGateIndependently()
    {
        Session session = Signed(new Assurance(AssuranceLevel.Aal2, PhishingResistant: false));
        HeldFactors held = Held(password: true, Factor.Totp);

        StepUpChallenge open = StepUp.On(
            session,
            Janus.Core.Policies.SystemDefault.Gates[StepUpAction.PrivacyExport],
            held,
            Noon);

        StepUpChallenge strict = StepUp.On(
            session,
            Janus.Core.Policies.AdministrativeOrganization.Gates[StepUpAction.PrivacyExport],
            held,
            Noon);

        Assert.Equal(StepUpOutcome.Satisfied, open.Outcome);
        Assert.Equal(StepUpOutcome.Enrol, strict.Outcome);
        Assert.True(strict.PhishingResistant);
        Assert.False(open.PhishingResistant);
    }

    private static HashSet<Factor> Set(params Factor[] factors) => [.. factors];

    private static Gate Gate(GateLevel level, bool phishingResistant) =>
        new(level, phishingResistant, Recency);

    private static HeldFactors Held(bool password, params Factor[] factors)
    {
        HashSet<Factor> held = [.. factors];

        if (password)
        {
            held.Add(Factor.Password);
        }

        return new HeldFactors(held, held, null);
    }

    private static SessionOrigin Origin() =>
        new("198.51.100.7", new DeviceDescription("Firefox", "Linux"), null);

    private static IReadOnlyList<IReadOnlyList<Factor>> Offered(StepUpChallenge challenge) =>
        challenge.Combinations;

    private static IEnumerable<Gate> Gates() =>
    [
        Gate(GateLevel.Reachable, phishingResistant: false),
        Gate(GateLevel.Reachable, phishingResistant: true),
        Gate(GateLevel.Aal1, phishingResistant: false),
        Gate(GateLevel.Aal2, phishingResistant: false),
        Gate(GateLevel.Aal2, phishingResistant: true),
    ];

    private static IEnumerable<HeldFactors> Accounts() =>
    [
        Held(password: false),
        Held(password: true),
        Held(password: false, Factor.Google),
        Held(password: true, Factor.Totp),
        Held(password: true, Factor.PhoneCode),
        Held(password: true, Factor.RecoveryCodes),
        Held(password: true, Factor.SecurityKey),
        Held(password: false, Factor.Passkey),
        Held(password: true, Factor.Passkey, Factor.Totp),
        Held(password: false, Factor.Totp),
    ];

    private StepUpChallenge Challenge(Gate gate, HeldFactors held) =>
        StepUp.On(Signed(null), gate, held, Noon);

    private Session Signed(Assurance? reached) =>
        Session.Begin(
            SessionId.New(TimeProvider.System),
            SubjectId.New(_randomness),
            reached ?? new Assurance(AssuranceLevel.Delegated, PhishingResistant: false),
            Origin(),
            Noon,
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            satisfiesEveryGate: false);

    private Authenticator Suspended(Factor factor, DateTimeOffset? completes = null)
    {
        Authenticator held = Enrolled(factor);

        held.Suspend(completes ?? Noon + TimeSpan.FromDays(7));

        return held;
    }

    private Authenticator Invalidated(Factor factor)
    {
        Authenticator held = Enrolled(factor);

        held.Invalidate();

        return held;
    }

    private Authenticator Enrolled(Factor factor)
    {
        var held = Authenticator.EnrollingTotp(
            AuthenticatorId.New(TimeProvider.System),
            SubjectId.New(_randomness),
            Label(),
            new byte[20],
            Noon);

        held.Confirm(Noon);

        return factor is Factor.Totp
            ? held
            : throw new ArgumentOutOfRangeException(nameof(factor), factor, "The helper enrols a code generator.");
    }

    private static CredentialLabel Label() =>
        CredentialLabel.TryParse("this phone", out CredentialLabel label)
            ? label
            : throw new Xunit.Sdk.XunitException("The label is one the chapter admits.");
}
