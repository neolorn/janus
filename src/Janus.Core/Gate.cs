using System;

namespace Janus.Core;

/// <summary>
/// What a step-up action costs under a policy: the tier the session has to reach,
/// whether the factors that reach it have to be phishing-resistant, and how recently
/// they have to have been presented.
/// </summary>
/// <param name="Level">The tier the session has to reach.</param>
/// <param name="PhishingResistant">Whether only phishing-resistant factors count.</param>
/// <param name="MaximumAge">How long ago the factors may have been presented.</param>
/// <remarks>
/// Implements chapter 10 section 4.1a, AUTH-STEP-002, AUTH-STEP-002a. Any combination
/// of the account's usable factors that reaches the gate satisfies it; a person who
/// cannot is offered enrolment or a loss report, never refused.
/// </remarks>
public sealed record Gate(GateLevel Level, bool PhishingResistant, TimeSpan MaximumAge);
