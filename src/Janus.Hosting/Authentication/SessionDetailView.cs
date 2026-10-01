using System;
using System.Text.Json.Serialization;
using Janus.Core;

namespace Janus.Hosting.Authentication;

/// <summary>
/// What a session says about itself.
/// </summary>
/// <param name="Subject">Whose session it is.</param>
/// <param name="AssuranceLevel">What it attained.</param>
/// <param name="PhishingResistant">Whether what attained it resists phishing.</param>
/// <param name="LastStrongAuthAt">When the level was last attained.</param>
/// <param name="ExpiresAt">The earlier of the idle and the absolute expiry.</param>
/// <param name="Landing">
/// The origin of the client a registration captured, on the session its terms step
/// established, and absent on any other.
/// </param>
/// <remarks>
/// Implements AUTH-SESS-002, AUTHZ-CACHE-002 and REG-SESS-008. No organization,
/// permission or role name is returned.
/// </remarks>
internal sealed record SessionDetailView(
    string Subject,
    AssuranceLevel AssuranceLevel,
    bool PhishingResistant,
    DateTimeOffset LastStrongAuthAt,
    DateTimeOffset ExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Landing)
{
    /// <summary>
    /// Reads a session.
    /// </summary>
    /// <param name="session">What the session says about itself.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public static SessionDetailView Of(SessionDetail session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new SessionDetailView(
            session.Subject.ToString(),
            session.AssuranceLevel,
            session.PhishingResistant,
            session.LastStrongAuthAt,
            session.ExpiresAt,
            session.Landing);
    }
}
