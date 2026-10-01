using System;

namespace Janus.Core;

/// <summary>
/// What a host reports of how far the caller's session has authenticated, where
/// authorization is consumed without this library's authentication.
/// </summary>
/// <param name="Level">The assurance level the session attained.</param>
/// <param name="PhishingResistant">Whether it was attained phishing-resistant.</param>
/// <param name="AttainedAt">When it was attained, which a gate's recency is judged from.</param>
/// <param name="Reachable">
/// The most the caller's account can reach, which a gate asking for what the account
/// can reach reads.
/// </param>
/// <remarks>Implements LIB-HOST-004 and AUTH-STEP-002.</remarks>
public sealed record AttainedAssurance(
    AssuranceLevel Level,
    bool PhishingResistant,
    DateTimeOffset AttainedAt,
    AssuranceLevel Reachable);
