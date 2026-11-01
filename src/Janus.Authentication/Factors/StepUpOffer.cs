using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a gate the session does not meet offers the account: every combination of its
/// usable factors that reaches the gate, or, where none does, what would change that.
/// </summary>
/// <remarks>
/// Implements AUTH-STEP-002 steps 2 and 3, AUTH-STEP-005 and AUTH-STEP-008 (D-243).
/// Whether the session meets the gate is judged before the offer is made, and not here
/// (<see cref="StepUp"/>): the offer admits and refuses nothing. It reads which of the
/// account's factors can be presented, and never asks what a factor is called.
/// </remarks>
internal static class StepUpOffer
{
    /// <summary>
    /// What an unmet gate offers an account holding these factors.
    /// </summary>
    /// <param name="held">What the account holds.</param>
    /// <param name="required">The tier a combination has to reach.</param>
    /// <param name="phishingResistant">Whether only relay-resistant factors count.</param>
    /// <param name="maximumAge">How long ago the proof may have been given.</param>
    /// <param name="reachable">The most the account can reach with what it holds.</param>
    /// <returns>
    /// The combinations to choose among, or the one of four answers an account that
    /// can present none is given.
    /// </returns>
    /// <exception cref="ArgumentNullException">The factors are absent.</exception>
    public static StepUpChallenge To(
        HeldFactors held,
        AssuranceLevel required,
        bool phishingResistant,
        TimeSpan maximumAge,
        Assurance reachable)
    {
        ArgumentNullException.ThrowIfNull(held);

        // AUTH-STEP-005: what a provider asserts is never offered at a step-up, though
        // at a delegated gate it would reach the level.
        IReadOnlyList<IReadOnlyList<Factor>> offered =
            [.. StepUp.Combinations(held.Usable)
                .Where(combination => !combination.Any(Asserted)
                    && Meets(combination, required, phishingResistant))];

        if (offered.Count > 0)
        {
            return new StepUpChallenge(
                StepUpOutcome.Present,
                required,
                phishingResistant,
                maximumAge,
                offered,
                null);
        }

        // Four answers and never a bare refusal: the account has never held what the
        // gate asks; it holds only what a provider asserts, which a step-up refuses, so
        // it signs in again with the provider (D-243); it holds it and cannot present
        // it; or it is already waiting for the report it made to complete
        // (AUTH-STEP-002).
        StepUpOutcome outcome = !Reaches(reachable, required, phishingResistant)
            ? StepUpOutcome.Enrol
            : held.LossCompletes is not null
                ? StepUpOutcome.LossPending
                : SignsInAgain(held, required)
                    ? StepUpOutcome.SignIn
                    : StepUpOutcome.ReportLoss;

        return new StepUpChallenge(
            outcome,
            required,
            phishingResistant,
            maximumAge,
            [],
            outcome is StepUpOutcome.LossPending ? held.LossCompletes : null);
    }

    /// <summary>
    /// Whether factors accepted together reach the gate a challenge was offered
    /// against.
    /// </summary>
    /// <param name="challenge">What the gate answered.</param>
    /// <param name="accepted">The factors accepted.</param>
    /// <returns>Whether what they prove together is the gate's level, and resists relay where it asks that.</returns>
    /// <exception cref="ArgumentNullException">The challenge or the factors are absent.</exception>
    public static bool ReachedBy(StepUpChallenge challenge, IReadOnlyCollection<Factor> accepted)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(accepted);

        return Meets([.. accepted], challenge.Required, challenge.PhishingResistant);
    }

    // AUTH-STEP-002 step 3 (D-243): a delegated gate unmet, and every factor the account
    // can present is one only a provider asserts.
    private static bool SignsInAgain(HeldFactors held, AssuranceLevel required) =>
        required is AssuranceLevel.Delegated
        && held.Usable.Count > 0
        && held.Usable.All(Asserted);

    private static bool Asserted(Factor factor) =>
        FactorCatalogue.Of(factor).AssuranceLevel is AssuranceLevel.Delegated;

    private static bool Meets(
        IReadOnlyList<Factor> combination,
        AssuranceLevel required,
        bool phishingResistant) =>
        Assurance.Proved([.. combination.Select(FactorCatalogue.Of)]) is { } proved
        && Reaches(proved, required, phishingResistant);

    private static bool Reaches(Assurance reached, AssuranceLevel required, bool phishingResistant) =>
        reached.Level >= required && (!phishingResistant || reached.PhishingResistant);
}
