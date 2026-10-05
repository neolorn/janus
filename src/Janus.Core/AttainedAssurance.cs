using System;

namespace Janus.Core;

/// <summary>
/// What a host reports of how far the caller's session has authenticated, where
/// authorization is consumed without this library's authentication.
/// </summary>
/// <param name="Aal1At">When <c>aal1</c> was last reached, or nothing where it never was.</param>
/// <param name="Aal2At">When <c>aal2</c> was last reached, or nothing where it never was.</param>
/// <param name="Aal3At">When <c>aal3</c> was last reached, or nothing where it never was.</param>
/// <param name="PhishingResistantAt">
/// When phishing resistance was last reached, or nothing where it never was.
/// </param>
/// <param name="Reachable">
/// The most the caller's account can reach, which a gate asking for what the account
/// can reach reads.
/// </param>
/// <remarks>
/// Implements LIB-HOST-004 and AUTH-STEP-002. A presentation renews the instant of each
/// level it reaches and of phishing resistance where it reaches it, and of nothing else,
/// as the session record does (AUTH-SESS-001).
/// </remarks>
public sealed record AttainedAssurance(
    DateTimeOffset? Aal1At,
    DateTimeOffset? Aal2At,
    DateTimeOffset? Aal3At,
    DateTimeOffset? PhishingResistantAt,
    AssuranceLevel Reachable);
