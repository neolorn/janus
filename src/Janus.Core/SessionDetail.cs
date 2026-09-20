using System;

namespace Janus.Core;

/// <summary>
/// What a session says about itself.
/// </summary>
/// <param name="Subject">Whose session it is.</param>
/// <param name="AssuranceLevel">What it attained.</param>
/// <param name="PhishingResistant">Whether what attained it resists phishing.</param>
/// <param name="LastStrongAuthAt">When the level was last attained.</param>
/// <param name="ExpiresAt">The earlier of the idle and the absolute expiry.</param>
/// <remarks>
/// Implements AUTH-SESS-002 and AUTHZ-CACHE-002. No organization is returned:
/// authorization resolves one from the resource and never from the session
/// (IDN-MEM-003). No permission or role name is returned either.
/// </remarks>
public sealed record SessionDetail(
    SubjectId Subject,
    AssuranceLevel AssuranceLevel,
    bool PhishingResistant,
    DateTimeOffset LastStrongAuthAt,
    DateTimeOffset ExpiresAt);
