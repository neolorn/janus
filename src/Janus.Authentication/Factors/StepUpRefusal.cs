using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What an operation answers when its gate is not met: the three values of the gate,
/// what the person does next, and the combinations of what they hold that would
/// satisfy it.
/// </summary>
/// <remarks>
/// Implements AUTH-STEP-001, AUTH-STEP-002 and chapter 9 <c>POST /auth/step-up</c>.
/// A person who cannot reach the gate is offered enrolment or a loss report, never
/// refused outright, which is why the outcome travels with the refusal.
/// </remarks>
internal static class StepUpRefusal
{
    /// <summary>
    /// Whether one challenge lets the operation proceed.
    /// </summary>
    /// <param name="challenge">What the gate answered.</param>
    /// <returns>Whether the gate is met.</returns>
    /// <exception cref="ArgumentNullException">The challenge is absent.</exception>
    public static bool Met(StepUpChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        return challenge.Outcome is StepUpOutcome.Satisfied;
    }

    /// <summary>
    /// The refusal one unmet gate produces.
    /// </summary>
    /// <param name="action">The gate name of chapter 10 section 5a.</param>
    /// <param name="challenge">What the gate answered.</param>
    /// <returns>The failure.</returns>
    /// <exception cref="ArgumentNullException">The action or the challenge is absent.</exception>
    public static Error Of(string action, StepUpChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(challenge);

        var details = new Dictionary<string, JsonElement>(capacity: 6, StringComparer.Ordinal)
        {
            ["action"] = JsonSerializer.SerializeToElement(action),
            ["level"] = JsonSerializer.SerializeToElement(WrittenName.Of(challenge.Required)),
            ["phishingResistant"] = JsonSerializer.SerializeToElement(challenge.PhishingResistant),
            ["outcome"] = JsonSerializer.SerializeToElement(WrittenName.Of(challenge.Outcome)),
            ["combinations"] = JsonSerializer.SerializeToElement(
                challenge.Combinations
                    .Select(combination => combination.Select(WrittenName.Of).ToArray())
                    .ToArray()),
        };

        if (challenge.LossCompletes is DateTimeOffset completes)
        {
            details["lossCompletes"] = JsonSerializer.SerializeToElement(completes);
        }

        return new Error(ErrorCodes.StepUpRequired, details);
    }
}
