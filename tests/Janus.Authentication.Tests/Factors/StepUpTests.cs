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
    // The head every library assembly's name carries, read from the core's
    // namespace so no string spells the product name (CONV-NAME-001).
    private static readonly string Library = typeof(Result).Namespace!.Split('.')[0] + ".";

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
            breakGlassReason: "The operator cannot be reached.");

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
    /// AUTH-STEP-002 AC3, AUTH-STEP-007: a gate whose level is <c>delegated</c> asks no
    /// maximum age, so the session of a social-only account that reached that level
    /// longer ago than the gate's maximum age is not challenged to set a password.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC3_AGateWhoseLevelIsDelegatedAsksNoMaximumAge() =>
        Assert.Equal(
            StepUpOutcome.Satisfied,
            StepUp.ToEnrol(
                    Signed(new Assurance(AssuranceLevel.Delegated, PhishingResistant: false)),
                    Gate(GateLevel.Reachable, phishingResistant: false),
                    Held(password: false, Factor.Google),
                    Factor.Password,
                    Noon + Recency + TimeSpan.FromDays(1))
                .Outcome);

    /// <summary>
    /// AUTH-STEP-002 AC3, AUTH-SESS-009: a gate whose level is <c>delegated</c> asks no
    /// maximum age and still counts nothing the session reached up to its last
    /// downgrade.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC3_AGateWhoseLevelIsDelegatedCountsNothingReachedUpToTheLastDowngrade()
    {
        Session downgraded = Signed(new Assurance(AssuranceLevel.Delegated, PhishingResistant: false));

        downgraded.Downgrade(Noon);

        StepUpChallenge asked = StepUp.ToEnrol(
            downgraded,
            Gate(GateLevel.Reachable, phishingResistant: false),
            Held(password: false, Factor.Google),
            Factor.Password,
            Noon + Recency + TimeSpan.FromDays(1));

        Assert.NotEqual(StepUpOutcome.Satisfied, asked.Outcome);
        Assert.True(asked.Downgraded);
    }

    /// <summary>
    /// AUTH-STEP-002 AC3, AUTH-STEP-007: only a gate whose level is <c>delegated</c>
    /// asks no maximum age, so an enrolment gated at a level above it is challenged
    /// once the session last reached that level longer ago than the maximum age.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC3_AnEnrolmentGateAboveDelegatedAsksItsMaximumAge()
    {
        Session session = Signed(new Assurance(AssuranceLevel.Aal1, PhishingResistant: false));
        Gate gate = Gate(GateLevel.Reachable, phishingResistant: false);
        HeldFactors held = Held(password: true);

        Assert.Equal(StepUpOutcome.Satisfied, StepUp.ToEnrol(session, gate, held, Factor.Passkey, Noon + Recency).Outcome);
        Assert.Equal(
            StepUpOutcome.Present,
            StepUp.ToEnrol(session, gate, held, Factor.Passkey, Noon + Recency + TimeSpan.FromSeconds(1)).Outcome);
    }

    /// <summary>
    /// AUTH-STEP-008 invariant 3: from every account state and for every gate, the
    /// answer is a move and never a bare refusal, and an offer never comes empty.
    /// </summary>
    [Fact]
    public void AUTH_STEP_008_AC3_EveryAccountStateAndGateHasAMove()
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
    /// AUTH-STEP-008 invariant 2: a gate is decided from the
    /// session record and what the account reaches, so two accounts reaching the same
    /// thing are asked the same thing whatever they hold.
    /// </summary>
    [Fact]
    public void AUTH_STEP_008_AC2_TwoAccountsReachingTheSameAreAskedTheSame()
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

    /// <summary>
    /// AUTH-STEP-002 AC1: a gate is a required tier, a phishing-resistance
    /// requirement and a maximum age, and carries nothing else; no value of it names
    /// a factor.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC1_AGateIsThreeValuesAndNamesNoFactor()
    {
        Assert.Equal(
            ["Level", "MaximumAge", "PhishingResistant"],
            [.. typeof(Gate)
                .GetProperties()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)]);

        Assert.DoesNotContain(
            typeof(Gate).GetProperties(),
            property => property.PropertyType.FullName!.Contains(
                nameof(Factor),
                StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTH-STEP-002 AC2: what a gate requires is read from the session record and
    /// what the account reaches, so a session that already proved it is not
    /// challenged whatever the account is enrolled in.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC2_TheDecisionReadsTheSessionAndWhatIsReachable()
    {
        Session proved = Signed(new Assurance(AssuranceLevel.Aal2, PhishingResistant: true));
        Gate gate = Gate(GateLevel.Aal2, phishingResistant: true);

        Assert.All(
            Accounts(),
            held => Assert.Equal(
                StepUpOutcome.Satisfied,
                StepUp.On(proved, gate, held, Noon).Outcome));
    }

    /// <summary>
    /// AUTH-STEP-002 AC6: the offer holds every combination that reaches the gate and
    /// none that falls short of it, the same account being offered nothing at all
    /// where the gate asks for relay resistance it cannot reach.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC6_NothingThatFallsShortIsOffered()
    {
        HeldFactors held = Held(password: true, Factor.Totp, Factor.PhoneCode);

        Assert.Equal(
            [[Factor.Password, Factor.Totp], [Factor.Password, Factor.PhoneCode]],
            Offered(Challenge(Gate(GateLevel.Aal2, phishingResistant: false), held)));
        Assert.Empty(Offered(Challenge(Gate(GateLevel.Aal2, phishingResistant: true), held)));
    }

    /// <summary>
    /// AUTH-STEP-004 AC3: the exception is the session's and dies with it, so the
    /// next session the same subject holds is asked what every other session is.
    /// </summary>
    [Fact]
    public void AUTH_STEP_004_AC3_TheExceptionDoesNotOutliveTheSession()
    {
        var subject = SubjectId.New(_randomness);
        Gate gate = Gate(GateLevel.Aal2, phishingResistant: true);

        Assert.Equal(
            StepUpOutcome.Satisfied,
            StepUp.On(Standing(subject, satisfiesEveryGate: true), gate, Held(password: true), Noon)
                .Outcome);

        Assert.Equal(
            StepUpOutcome.Enrol,
            StepUp.On(Standing(subject, satisfiesEveryGate: false), gate, Held(password: true), Noon)
                .Outcome);
    }

    /// <summary>
    /// AUTH-STEP-005 AC2: a subject holding only what a provider asserted is told to
    /// enrol, and enrolling either entry that begins an authentication on its own
    /// lifts them past the gate. Which entry that is the catalogue says and this rule
    /// does not.
    /// </summary>
    [Fact]
    public void AUTH_STEP_005_AC2_ASocialOnlySubjectIsToldToEnrol()
    {
        Gate gate = Gate(GateLevel.Reachable, phishingResistant: false);
        StepUpChallenge challenge = Challenge(gate, Held(password: false, Factor.Google, Factor.Apple));

        Assert.Equal(StepUpOutcome.Enrol, challenge.Outcome);
        Assert.Equal(AssuranceLevel.Aal1, challenge.Required);
        Assert.Empty(challenge.Combinations);

        Assert.All(
            new[] { Factor.Password, Factor.Passkey },
            enrolled => Assert.Equal(
                StepUpOutcome.Present,
                Challenge(gate, Held(password: false, Factor.Google, enrolled)).Outcome));
    }

    /// <summary>
    /// AUTH-STEP-005 AC3: what the provider asserted decides nothing, so a session it
    /// began passes no gate on its word and holding its credential changes no offer.
    /// </summary>
    [Fact]
    public void AUTH_STEP_005_AC3_WhatTheProviderAssertedDecidesNothing()
    {
        Gate gate = Gate(GateLevel.Reachable, phishingResistant: false);
        Session signed = Signed(new Assurance(AssuranceLevel.Delegated, PhishingResistant: false));

        Assert.All(
            new[] { Factor.Google, Factor.Apple },
            provider => Assert.Equal(
                StepUpOutcome.Enrol,
                StepUp.On(signed, gate, Held(password: false, provider), Noon).Outcome));

        Assert.Equal(
            Offered(StepUp.On(signed, gate, Held(password: true), Noon)),
            Offered(StepUp.On(signed, gate, Held(password: true, Factor.Google, Factor.Apple), Noon)));
    }

    /// <summary>
    /// AUTH-FACT-002a AC4: the prompt on a session a provider began offers only the
    /// account's own factors that reach the declared tier; the provider's credential
    /// is in none of them.
    /// </summary>
    [Fact]
    public void AUTH_FACT_002a_AC4_TheProvidersCredentialIsInNoCombination() =>
        Assert.Equal(
            [[Factor.Password, Factor.Totp]],
            Offered(StepUp.On(
                Signed(new Assurance(AssuranceLevel.Delegated, PhishingResistant: false)),
                Gate(GateLevel.Aal2, phishingResistant: false),
                Held(password: true, Factor.Google, Factor.Totp),
                Noon)));

    /// <summary>
    /// AUTH-SESS-009 AC2: a session standing below a requirement that has since been
    /// raised passes no gate under it and is told what to present again.
    /// </summary>
    [Fact]
    public void AUTH_SESS_009_AC2_ASessionBelowTheRaisedRequirementIsAskedAgain()
    {
        StepUpChallenge challenge = StepUp.On(
            Signed(new Assurance(AssuranceLevel.Aal1, PhishingResistant: false)),
            Janus.Core.Policies.AdministrativeOrganization.Gates[StepUpAction.PrivacyExport],
            Held(password: true, Factor.SecurityKey),
            Noon);

        Assert.Equal(StepUpOutcome.Present, challenge.Outcome);
        Assert.Equal([[Factor.Password, Factor.SecurityKey]], Offered(challenge));
    }

    /// <summary>
    /// AUTH-SESS-009 AC6, AUTH-STEP-002 AC3: a session that proved the gate a minute
    /// before it is downgraded is asked a presentation at its next gated action, the
    /// gate saying that the downgrade alone keeps it unmet; a presentation made after
    /// the downgrade passes the gate, and what the session attained before it is as it
    /// was reached.
    /// </summary>
    [Fact]
    public void AUTH_SESS_009_AC6_ADowngradedSessionIsAskedAPresentationThatLiftsIt()
    {
        var reached = new Assurance(AssuranceLevel.Aal2, PhishingResistant: true);
        Gate gate = Gate(GateLevel.Aal2, phishingResistant: true);
        HeldFactors held = Held(password: true, Factor.Passkey);
        Session session = Signed(reached);

        session.Downgrade(Noon + TimeSpan.FromMinutes(1));

        StepUpChallenge asked = StepUp.On(session, gate, held, Noon + TimeSpan.FromMinutes(2));

        Assert.Equal((StepUpOutcome.Present, true), (asked.Outcome, asked.Downgraded));
        Assert.Equal(
            (AssuranceLevel.Aal2, (DateTimeOffset?)Noon, (DateTimeOffset?)Noon, (DateTimeOffset?)Noon),
            (session.Attained, session.Aal1At, session.Aal2At, session.PhishingResistantAt));

        session.Present(reached, Noon + TimeSpan.FromMinutes(3));

        StepUpChallenge lifted = StepUp.On(session, gate, held, Noon + TimeSpan.FromMinutes(4));

        Assert.Equal((StepUpOutcome.Satisfied, false), (lifted.Outcome, lifted.Downgraded));
    }

    /// <summary>
    /// AUTH-STEP-002 AC3, AUTHZ-GATE-005: a gate a downgraded session would not meet
    /// whatever its downgrade, its proof having aged, is not one the downgrade alone
    /// keeps unmet; and a session never downgraded is judged as before.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC3_ProofAttainedUpToTheLastDowngradeIsNotCounted()
    {
        var reached = new Assurance(AssuranceLevel.Aal2, PhishingResistant: true);
        Gate gate = Gate(GateLevel.Aal2, phishingResistant: true);
        HeldFactors held = Held(password: true, Factor.Passkey);
        Session downgraded = Signed(reached);
        Session standing = Signed(reached);

        downgraded.Downgrade(Noon);

        StepUpChallenge atOnce = StepUp.On(downgraded, gate, held, Noon + TimeSpan.FromMinutes(1));
        StepUpChallenge aged = StepUp.On(downgraded, gate, held, Noon + TimeSpan.FromMinutes(16));

        Assert.Equal((StepUpOutcome.Present, true), (atOnce.Outcome, atOnce.Downgraded));
        Assert.Equal((StepUpOutcome.Present, false), (aged.Outcome, aged.Downgraded));
        Assert.Equal(
            StepUpOutcome.Satisfied,
            StepUp.On(standing, gate, held, Noon + TimeSpan.FromMinutes(1)).Outcome);
    }

    /// <summary>
    /// AUTH-SESS-001 AC3, AUTH-STEP-002 AC3: a presentation writes the instant of each
    /// level it reaches, its own and every lower one, and of phishing resistance where
    /// it reaches it, and changes no instant of what it does not reach; so a bare
    /// password under a session that reached <c>aal2</c> earlier meets an
    /// <c>aal1</c> gate and no <c>aal2</c> gate whose maximum age has passed, and two
    /// factors that resist no relay renew <c>aal2</c> and not phishing resistance.
    /// </summary>
    [Fact]
    public void AUTH_SESS_001_AC3_APresentationRenewsOnlyWhatItReaches()
    {
        HeldFactors held = Held(password: true, Factor.Totp, Factor.Passkey);
        Session session = Signed(new Assurance(AssuranceLevel.Aal2, PhishingResistant: true));
        DateTimeOffset password = Noon + TimeSpan.FromMinutes(10);
        DateTimeOffset generated = Noon + TimeSpan.FromMinutes(20);

        session.Present(new Assurance(AssuranceLevel.Aal1, PhishingResistant: false), password);

        StepUpOutcome single = On(GateLevel.Aal1, phishingResistant: false, Noon + TimeSpan.FromMinutes(16));
        StepUpOutcome aged = On(GateLevel.Aal2, phishingResistant: false, Noon + TimeSpan.FromMinutes(16));

        Assert.Equal(
            (AssuranceLevel.Aal2, password, (DateTimeOffset?)password, (DateTimeOffset?)Noon, null, (DateTimeOffset?)Noon),
            (session.Attained, session.DelegatedAt, session.Aal1At, session.Aal2At, session.Aal3At, session.PhishingResistantAt));
        Assert.Equal((StepUpOutcome.Satisfied, StepUpOutcome.Present), (single, aged));

        session.Present(new Assurance(AssuranceLevel.Aal2, PhishingResistant: false), generated);

        Assert.Equal(
            (generated, (DateTimeOffset?)generated, (DateTimeOffset?)generated, null, (DateTimeOffset?)Noon),
            (session.DelegatedAt, session.Aal1At, session.Aal2At, session.Aal3At, session.PhishingResistantAt));
        Assert.Equal(
            (StepUpOutcome.Satisfied, StepUpOutcome.Present),
            (On(GateLevel.Aal2, phishingResistant: false, Noon + TimeSpan.FromMinutes(21)),
                On(GateLevel.Aal2, phishingResistant: true, Noon + TimeSpan.FromMinutes(21))));

        StepUpOutcome On(GateLevel level, bool phishingResistant, DateTimeOffset at) =>
            StepUp.On(session, Gate(level, phishingResistant), held, at).Outcome;
    }

    /// <summary>
    /// AUTH-SESS-009 (D-191): what a presentation reaches after a downgrade counts from
    /// then and lifts nothing it does not reach, so a bare password under a downgraded
    /// session that reached <c>aal2</c> before meets an <c>aal1</c> gate and leaves an
    /// <c>aal2</c> gate unmet for the downgrade alone.
    /// </summary>
    [Fact]
    public void AUTH_SESS_009_AC6_APresentationAfterADowngradeLiftsOnlyWhatItReaches()
    {
        HeldFactors held = Held(password: true, Factor.Totp);
        Session session = Signed(new Assurance(AssuranceLevel.Aal2, PhishingResistant: false));

        session.Downgrade(Noon + TimeSpan.FromMinutes(1));
        session.Present(new Assurance(AssuranceLevel.Aal1, PhishingResistant: false), Noon + TimeSpan.FromMinutes(2));

        StepUpChallenge single = StepUp.On(session, Gate(GateLevel.Aal1, phishingResistant: false), held, Noon + TimeSpan.FromMinutes(3));
        StepUpChallenge strong = StepUp.On(session, Gate(GateLevel.Aal2, phishingResistant: false), held, Noon + TimeSpan.FromMinutes(3));

        Assert.Equal(StepUpOutcome.Satisfied, single.Outcome);
        Assert.Equal((StepUpOutcome.Present, true), (strong.Outcome, strong.Downgraded));
        Assert.Equal(Noon, session.Aal2At);
    }

    /// <summary>
    /// AUTH-SESS-009 AC5: a session derived from a record that stands downgraded passes
    /// no gate either, and one derived after a presentation lifted the downgrade does.
    /// </summary>
    [Fact]
    public void AUTH_SESS_009_AC5_ASessionDerivedFromADowngradedRecordPassesNoGate()
    {
        var reached = new Assurance(AssuranceLevel.Aal2, PhishingResistant: true);
        Gate gate = Gate(GateLevel.Aal2, phishingResistant: true);
        HeldFactors held = Held(password: true, Factor.Passkey);
        Session record = Signed(reached);

        record.Downgrade(Noon + TimeSpan.FromMinutes(1));

        Session derived = Derived(record, Noon + TimeSpan.FromMinutes(2));

        record.Present(reached, Noon + TimeSpan.FromMinutes(3));

        Session after = Derived(record, Noon + TimeSpan.FromMinutes(4));

        Assert.Equal(
            StepUpOutcome.Present,
            StepUp.On(derived, gate, held, Noon + TimeSpan.FromMinutes(5)).Outcome);
        Assert.Equal(
            StepUpOutcome.Satisfied,
            StepUp.On(after, gate, held, Noon + TimeSpan.FromMinutes(5)).Outcome);

        static Session Derived(Session record, DateTimeOffset at) =>
            record.Derive(SessionId.New(TimeProvider.System), SessionType.PerApp, Origin(), at, TimeSpan.FromDays(1));
    }

    /// <summary>
    /// AUTH-SESS-009 AC5 (D-191): a bare password presented on a downgraded record that
    /// reached <c>aal2</c> before lifts nothing of <c>aal2</c>, so a session derived
    /// from it afterwards passes no <c>aal2</c> gate.
    /// </summary>
    [Fact]
    public void AUTH_SESS_009_AC5_ASessionDerivedAfterABarePasswordOnADowngradedRecordPassesNoAal2Gate()
    {
        HeldFactors held = Held(password: true, Factor.Totp);
        Session record = Signed(new Assurance(AssuranceLevel.Aal2, PhishingResistant: false));

        record.Downgrade(Noon + TimeSpan.FromMinutes(1));
        record.Present(new Assurance(AssuranceLevel.Aal1, PhishingResistant: false), Noon + TimeSpan.FromMinutes(2));

        Session derived = record.Derive(
            SessionId.New(TimeProvider.System),
            SessionType.PerApp,
            Origin(),
            Noon + TimeSpan.FromMinutes(3),
            TimeSpan.FromDays(1));

        Assert.Equal(
            StepUpOutcome.Present,
            StepUp.On(derived, Gate(GateLevel.Aal2, phishingResistant: false), held, Noon + TimeSpan.FromMinutes(4)).Outcome);
    }

    /// <summary>
    /// AUTH-STEP-008 invariant 4: removing an authenticator is gated on the tier the
    /// account reaches and never on the authenticator itself, so the passkey that is
    /// no longer in its owner's hands is in none of the combinations offered to
    /// remove it.
    /// </summary>
    [Fact]
    public void AUTH_STEP_008_AC4_RemovingAnAuthenticatorIsNotGatedOnPresentingIt()
    {
        StepUpChallenge challenge = StepUp.On(
            Signed(null),
            Janus.Core.Policies.SystemDefault.Gates[StepUpAction.FactorRemove],
            new HeldFactors(
                Set(Factor.Password, Factor.Totp),
                Set(Factor.Password, Factor.Passkey),
                null),
            Noon);

        Assert.Equal(StepUpOutcome.Present, challenge.Outcome);
        Assert.DoesNotContain(
            Offered(challenge).SelectMany(combination => combination),
            factor => factor is Factor.Passkey);
    }

    /// <summary>
    /// AUTH-STEP-008 invariant 5: what an account reaches does not fall when a loss
    /// is reported, only when the window completes and the authenticator is
    /// invalidated. The notification that accompanies the fall is AUTH-RECOV-007's.
    /// </summary>
    [Fact]
    public void AUTH_STEP_008_AC5_ReachableAssuranceFallsOnlyOnInvalidation()
    {
        Assert.Equal(
            AssuranceLevel.Aal2,
            StepUp.Reachable(HeldFactors.Of([Suspended(Factor.Totp)], password: true).Standing).Level);

        Assert.Equal(
            AssuranceLevel.Aal1,
            StepUp.Reachable(HeldFactors.Of([Invalidated(Factor.Totp)], password: true).Standing).Level);
    }

    /// <summary>
    /// AUTH-STEP-008 invariant 6: what a combination proves is what the table of
    /// AUTH-SESS-005a assigns it and never more, whichever way the session came
    /// about.
    /// </summary>
    [Fact]
    public void AUTH_STEP_008_AC6_NothingProvesMoreThanWhatWasPresented()
    {
        foreach (HeldFactors held in Accounts())
        {
            foreach (Gate gate in Gates())
            {
                StepUpChallenge challenge = Challenge(gate, held);

                Assert.All(
                    Offered(challenge),
                    combination => Assert.True(
                        Assurance.Proved([.. combination.Select(FactorCatalogue.Of)]) is { } proved
                        && proved.Level >= challenge.Required
                        && (!challenge.PhishingResistant || proved.PhishingResistant)));
            }
        }
    }

    /// <summary>
    /// AUTH-STEP-008 invariant 7: the tier a gate asks for is read from the policy
    /// and from what the account reaches, and from no second anchor, so the same
    /// account under the same policy is asked the same thing however it signed in.
    /// </summary>
    [Fact]
    public void AUTH_STEP_008_AC7_OneAnchorDecidesWhatIsAsked()
    {
        Gate gate = Gate(GateLevel.Reachable, phishingResistant: false);
        HeldFactors held = Held(password: true, Factor.Totp);

        Assert.All(
            new Assurance?[]
            {
                null,
                new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
                new Assurance(AssuranceLevel.Delegated, PhishingResistant: false),
            },
            reached => Assert.Equal(
                AssuranceLevel.Aal2,
                StepUp.On(Signed(reached), gate, held, Noon).Required));
    }

    /// <summary>
    /// AUTH-STEP-002 AC8: a gate is a tier, a resistance requirement and an age,
    /// declared where the catalogue cannot be seen, so registering an entry reaches
    /// no value any gate carries.
    /// </summary>
    [Fact]
    public void AUTH_STEP_002_AC8_RegisteringANewFactorTypeChangesNoGate()
    {
        Gate gate = Janus.Core.Policies.AdministrativeOrganization.Gates[StepUpAction.PrivacyExport];

        Assert.Equal(GateLevel.Aal2, gate.Level);
        Assert.True(gate.PhishingResistant);

        Assert.DoesNotContain(
            typeof(Gate).Assembly.GetReferencedAssemblies(),
            referenced => referenced.Name!.StartsWith(Library, StringComparison.Ordinal));
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
        new("198.51.100.7", new DeviceDescription("Firefox", "Linux"));

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

    private static Session Standing(SubjectId subject, bool satisfiesEveryGate) =>
        Session.Begin(
            SessionId.New(TimeProvider.System),
            subject,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            Origin(),
            Noon,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1),
            satisfiesEveryGate ? "The operator cannot be reached." : null);

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
            breakGlassReason: null);

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
