using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What an operation answers when its gate is not met: the three values of the gate,
/// what the person does next, the combinations of what they hold that would satisfy
/// it, and when a pending loss report completes.
/// </summary>
/// <remarks>
/// Implements AUTH-STEP-001, AUTH-STEP-002, BFF-STEP-001 and chapter 9
/// <c>POST /auth/step-up</c>, whose shape this is: <c>required</c> carries
/// <c>level</c>, <c>phishingResistant</c> and <c>maxAge</c> in whole seconds, beside
/// <c>outcome</c>, <c>options</c> and <c>pendingUntil</c>, which is null unless the
/// outcome is <c>pending</c>. A person who cannot reach the gate is offered enrolment
/// or a loss report, never refused outright, which is why the outcome travels with the
/// refusal. The gate's name does not: the caller knows what it asked for.
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
    /// <param name="challenge">What the gate answered.</param>
    /// <returns>The failure.</returns>
    /// <exception cref="ArgumentNullException">The challenge is absent.</exception>
    public static Error Of(StepUpChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var required = new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
        {
            ["level"] = JsonSerializer.SerializeToElement(WrittenName.Of(challenge.Required)),
            ["phishingResistant"] = JsonSerializer.SerializeToElement(challenge.PhishingResistant),
            ["maxAge"] = JsonSerializer.SerializeToElement((long)challenge.MaximumAge.TotalSeconds),
        };

        var details = new Dictionary<string, JsonElement>(capacity: 4, StringComparer.Ordinal)
        {
            ["required"] = JsonSerializer.SerializeToElement(required),
            ["outcome"] = JsonSerializer.SerializeToElement(WrittenName.Of(challenge.Outcome)),
            ["options"] = JsonSerializer.SerializeToElement(
                challenge.Combinations
                    .Select(combination => combination.Select(WrittenName.Of).ToArray())
                    .ToArray()),
            ["pendingUntil"] = JsonSerializer.SerializeToElement(challenge.LossCompletes),
        };

        return new Error(ErrorCodes.StepUpRequired, details);
    }
}
