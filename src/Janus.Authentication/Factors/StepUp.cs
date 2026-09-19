using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a step-up gate asks of a session, and what the account may present to reach
/// it.
/// </summary>
/// <remarks>
/// Implements AUTH-STEP-002, AUTH-STEP-004, AUTH-STEP-005, AUTH-STEP-006,
/// AUTH-STEP-007 and AUTH-STEP-008. The decision reads the session record and the
/// account's reachable assurance; the offer reads which of the account's factors can
/// be presented. Neither asks what a factor is called.
/// </remarks>
internal static class StepUp
{
    /// <summary>
    /// The highest tier the account can reach with what it holds, and whether what
    /// reaches it resists relay.
    /// </summary>
    /// <param name="standing">The factors still standing against the account.</param>
    /// <returns>
    /// What the account can reach, which is <c>delegated</c> and not resistant where
    /// it can reach nothing of ours.
    /// </returns>
    /// <exception cref="ArgumentNullException">The set is absent.</exception>
    public static Assurance Reachable(IReadOnlySet<Factor> standing)
    {
        ArgumentNullException.ThrowIfNull(standing);

        // A one-use secret stands in for a second factor at a sign-in and at a gate,
        // and does not make the account an account that reaches two factors: it would
        // be spent at every gate that counted it (AUTH-STEP-006).
        HashSet<Factor> counting =
            [.. standing.Where(factor => !FactorCatalogue.Of(factor).SingleUse)];

        return Combinations(counting)
            .Select(combination => Assurance.Proved(Properties(combination)))
            .Where(reached => reached is not null)
            .Select(reached => reached!.Value)
            .DefaultIfEmpty(new Assurance(AssuranceLevel.Delegated, PhishingResistant: false))
            .Aggregate(Higher);
    }

    /// <summary>
    /// What the gate bound to an action asks of this session.
    /// </summary>
    /// <param name="session">The session the action is exercised on.</param>
    /// <param name="gate">The three values the principal's policy sets for it.</param>
    /// <param name="held">What the account holds.</param>
    /// <param name="now">The instant the gate is evaluated at.</param>
    /// <returns>What is required, and what would satisfy it.</returns>
    /// <exception cref="ArgumentNullException">The session, the gate or the factors are absent.</exception>
    public static StepUpChallenge On(
        Session session,
        Gate gate,
        HeldFactors held,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(held);

        Assurance reachable = Reachable(held.Standing);

        return Answered(
            session,
            gate,
            held,
            Stated(gate, reachable),
            gate.PhishingResistant,
            reachable,
            now);
    }

    /// <summary>
    /// What the gate on enrolling a credential asks of this session, which is the
    /// lower of what the account can reach and what the credential itself would
    /// contribute (AUTH-STEP-007).
    /// </summary>
    /// <param name="session">The session the enrolment is made from.</param>
    /// <param name="gate">The gate the policy binds to enrolment, read for its recency.</param>
    /// <param name="held">What the account holds.</param>
    /// <param name="enrolling">The catalogue entry being enrolled.</param>
    /// <param name="now">The instant the gate is evaluated at.</param>
    /// <returns>What is required, and what would satisfy it.</returns>
    /// <exception cref="ArgumentNullException">The session, the gate or the factors are absent.</exception>
    public static StepUpChallenge ToEnrol(
        Session session,
        Gate gate,
        HeldFactors held,
        Factor enrolling,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(held);

        Assurance reachable = Reachable(held.Standing);
        FactorProperties contribution = FactorCatalogue.Of(enrolling);

        // The person who holds one factor adds a second with the one they hold; the
        // person who holds none sets their first with nothing (AUTH-STEP-007).
        AssuranceLevel required = contribution.AssuranceLevel < reachable.Level
            ? contribution.AssuranceLevel
            : reachable.Level;

        return Answered(
            session,
            gate,
            held,
            required,
            contribution.IsPhishingResistant && reachable.PhishingResistant,
            reachable,
            now);
    }

    private static StepUpChallenge Answered(
        Session session,
        Gate gate,
        HeldFactors held,
        AssuranceLevel required,
        bool phishingResistant,
        Assurance reachable,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (Proved(session, gate, required, phishingResistant, now))
        {
            return new StepUpChallenge(StepUpOutcome.Satisfied, required, phishingResistant, [], null);
        }

        IReadOnlyList<IReadOnlyList<Factor>> offered =
            [.. Combinations(held.Usable).Where(combination => Meets(combination, required, phishingResistant))];

        if (offered.Count > 0)
        {
            return new StepUpChallenge(StepUpOutcome.Present, required, phishingResistant, offered, null);
        }

        // Three answers and never a bare refusal: the account has never held what the
        // gate asks; it holds it and cannot present it; or it is already waiting for
        // the report it made to complete (AUTH-STEP-002).
        StepUpOutcome outcome = !Reaches(reachable, required, phishingResistant)
            ? StepUpOutcome.Enrol
            : held.LossCompletes is null
                ? StepUpOutcome.ReportLoss
                : StepUpOutcome.LossPending;

        return new StepUpChallenge(
            outcome,
            required,
            phishingResistant,
            [],
            outcome is StepUpOutcome.LossPending ? held.LossCompletes : null);
    }

    // The emergency credential satisfies every gate for the session's lifetime, which
    // is what an emergency credential is for; the alerting is the control that pays
    // for it (AUTH-STEP-004).
    private static bool Proved(
        Session session,
        Gate gate,
        AssuranceLevel required,
        bool phishingResistant,
        DateTimeOffset now) =>
        session.SatisfiesEveryGate
        || (session.Attained >= required
            && Within(session.AttainedAt, gate.MaximumAge, now)
            && (!phishingResistant
                || (session.PhishingResistant
                    && session.PhishingResistantAt is { } proved
                    && Within(proved, gate.MaximumAge, now))));

    private static bool Within(DateTimeOffset at, TimeSpan age, DateTimeOffset now) =>
        now - at <= age;

    private static AssuranceLevel Stated(Gate gate, Assurance reachable) =>
        gate.Level switch
        {
            GateLevel.Aal1 => AssuranceLevel.Aal1,
            GateLevel.Aal2 => AssuranceLevel.Aal2,

            // The gate asks for the most the account can do and never more, and never
            // for less than one factor (AUTH-STEP-002a).
            _ => reachable.Level < AssuranceLevel.Aal1 ? AssuranceLevel.Aal1 : reachable.Level,
        };

    // A combination is one factor that begins an authentication, and at most one that
    // stands beside it: nothing in the table reaches further with a third.
    private static IReadOnlyList<IReadOnlyList<Factor>> Combinations(IReadOnlySet<Factor> factors)
    {
        List<Factor> primaries =
            [.. factors.Where(factor => FactorCatalogue.Of(factor).CanBePrimary).Order()];
        List<Factor> seconds =
            [.. factors.Where(factor => FactorCatalogue.Of(factor).CanBeSecondFactor).Order()];

        return
        [
            .. primaries.Select(primary => (IReadOnlyList<Factor>)[primary]),
            .. primaries.SelectMany(
                _ => seconds,
                (primary, second) => (IReadOnlyList<Factor>)[primary, second]),
        ];
    }

    private static bool Meets(
        IReadOnlyList<Factor> combination,
        AssuranceLevel required,
        bool phishingResistant) =>
        Assurance.Proved(Properties(combination)) is { } proved
        && Reaches(proved, required, phishingResistant);

    private static bool Reaches(Assurance reached, AssuranceLevel required, bool phishingResistant) =>
        reached.Level >= required && (!phishingResistant || reached.PhishingResistant);

    private static IReadOnlyCollection<FactorProperties> Properties(IReadOnlyList<Factor> combination) =>
        [.. combination.Select(FactorCatalogue.Of)];

    private static Assurance Higher(Assurance held, Assurance candidate) =>
        new(
            candidate.Level > held.Level ? candidate.Level : held.Level,
            candidate.PhishingResistant || held.PhishingResistant);
}
