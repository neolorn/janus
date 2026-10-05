using System;

namespace Janus.Core;

/// <summary>
/// What a session says about itself.
/// </summary>
/// <param name="Subject">Whose session it is.</param>
/// <param name="AssuranceLevel">The highest level it has reached.</param>
/// <param name="PhishingResistant">Whether it has reached phishing resistance.</param>
/// <param name="LastStrongAuthAt">
/// When <c>aal2</c> or above was last reached, or nothing where it never was.
/// </param>
/// <param name="ExpiresAt">The earlier of the idle and the absolute expiry.</param>
/// <param name="Landing">
/// The origin (scheme, host and port) of the registered address of the client a
/// registration captured, kept on the session its terms step established; nothing on
/// any other session (REG-SESS-008, API-REDIR-002).
/// </param>
/// <remarks>
/// Implements AUTH-SESS-001, AUTH-SESS-002 and AUTHZ-CACHE-002. No organization is returned:
/// authorization resolves one from the resource and never from the session
/// (IDN-MEM-003). No permission or role name is returned either.
/// </remarks>
public sealed record SessionDetail(
    SubjectId Subject,
    AssuranceLevel AssuranceLevel,
    bool PhishingResistant,
    DateTimeOffset? LastStrongAuthAt,
    DateTimeOffset ExpiresAt,
    string? Landing);
