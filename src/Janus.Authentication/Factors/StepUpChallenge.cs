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
/// <param name="MaximumAge">
/// How long ago the proof may have been given, as the policy states it for the action;
/// a gate whose level is <c>delegated</c> does not ask it (AUTH-STEP-007).
/// </param>
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
    DateTimeOffset? LossCompletes)
{
    /// <summary>
    /// Whether the session would meet the gate but for proof it last reached up to its last
    /// downgrade, which a capability names <c>reauthenticate</c> (AUTH-SESS-009,
    /// AUTHZ-GATE-005).
    /// </summary>
    public bool Downgraded { get; init; }
}
