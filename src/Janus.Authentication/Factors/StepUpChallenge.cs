using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a gate answered: whether anything is required, what the subject may present,
/// and where nothing can be presented, what would change that.
/// </summary>
/// <param name="Outcome">What the gate asks.</param>
/// <param name="Required">The tier a combination has to reach.</param>
/// <param name="PhishingResistant">Whether only relay-resistant factors count.</param>
/// <param name="MaximumAge">How long ago the proof may have been given.</param>
/// <param name="Combinations">
/// Every combination of the account's usable factors that reaches the gate, and none
/// that does not. Empty unless the outcome is
/// <see cref="StepUpOutcome.Present"/>.
/// </param>
/// <param name="LossCompletes">
/// When a pending loss report completes, where one is pending.
/// </param>
/// <remarks>Implements AUTH-STEP-002 and AUTH-STEP-007.</remarks>
internal sealed record StepUpChallenge(
    StepUpOutcome Outcome,
    AssuranceLevel Required,
    bool PhishingResistant,
    TimeSpan MaximumAge,
    IReadOnlyList<IReadOnlyList<Factor>> Combinations,
    DateTimeOffset? LossCompletes);
