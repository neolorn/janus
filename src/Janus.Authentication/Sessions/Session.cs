using System;
using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// The server-side record every credential derives from. Revoking it invalidates
/// everything derived from it.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-001, AUTH-SESS-002, AUTH-SESS-004 and AUTH-SESS-005. The
/// record holds the properties an authentication reached and never the factors that
/// reached them: factor identity is the audit trail's, so adding a factor edits no
/// rule that reads a session.
/// </remarks>
internal sealed class Session
{
    private Session(
        SessionId id,
        SessionId spine,
        SessionType type,
        SubjectId subject,
        Assurance reached,
        SessionOrigin origin,
        DateTimeOffset at,
        TimeSpan inactivity,
        DateTimeOffset absolute,
        bool satisfiesEveryGate)
    {
        Id = id;
        Spine = spine;
        Type = type;
        Subject = subject;
        CreatedAt = at;
        LastSeenAt = at;
        Attained = reached.Level;
        AttainedAt = at;
        PhishingResistant = reached.PhishingResistant;
        PhishingResistantAt = reached.PhishingResistant ? at : null;
        Origin = origin;
        LastSeen = origin;
        IdleExpiry = at + inactivity;
        AbsoluteExpiry = absolute;
        SatisfiesEveryGate = satisfiesEveryGate;
    }

    /// <summary>Which session.</summary>
    public SessionId Id { get; }

    /// <summary>
    /// The record this derives from, which is itself where it is the record.
    /// </summary>
    public SessionId Spine { get; }

    /// <summary>Which of the three kinds it is.</summary>
    public SessionType Type { get; }

    /// <summary>Whose session it is.</summary>
    public SubjectId Subject { get; }

    /// <summary>When it began.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it was last used.</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>The tier the authentication reached.</summary>
    public AssuranceLevel Attained { get; private set; }

    /// <summary>When that tier was reached.</summary>
    public DateTimeOffset AttainedAt { get; private set; }

    /// <summary>Whether what reached it resisted credential relay.</summary>
    public bool PhishingResistant { get; private set; }

    /// <summary>When that was last proved.</summary>
    public DateTimeOffset? PhishingResistantAt { get; private set; }

    /// <summary>Where it began.</summary>
    public SessionOrigin Origin { get; }

    /// <summary>Where it was last used.</summary>
    public SessionOrigin LastSeen { get; private set; }

    /// <summary>When it lapses without use.</summary>
    public DateTimeOffset IdleExpiry { get; private set; }

    /// <summary>When it ends whatever happens.</summary>
    public DateTimeOffset AbsoluteExpiry { get; }

    /// <summary>When it was ended, where it has been.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>
    /// Whether the session passes every gate and the stated floor for its lifetime,
    /// which only the emergency path sets and nothing else does.
    /// </summary>
    public bool SatisfiesEveryGate { get; }

    /// <summary>
    /// The record an authentication creates.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose session it is.</param>
    /// <param name="reached">What the authentication reached.</param>
    /// <param name="origin">Where it began.</param>
    /// <param name="at">When it began.</param>
    /// <param name="inactivity">How long it survives without use.</param>
    /// <param name="absolute">How long it survives at all.</param>
    /// <param name="satisfiesEveryGate">
    /// Whether it passes every gate and the stated floor for its lifetime, which only
    /// the emergency path sets (AUTH-SESS-005b, AUTH-STEP-004).
    /// </param>
    /// <returns>The session.</returns>
    /// <exception cref="ArgumentNullException">The origin is absent.</exception>
    public static Session Begin(
        SessionId id,
        SubjectId subject,
        Assurance reached,
        SessionOrigin origin,
        DateTimeOffset at,
        TimeSpan inactivity,
        TimeSpan absolute,
        bool satisfiesEveryGate)
    {
        ArgumentNullException.ThrowIfNull(origin);

        return new Session(
            id,
            id,
            SessionType.Auth,
            subject,
            reached,
            origin,
            at,
            inactivity,
            at + absolute,
            satisfiesEveryGate);
    }

    /// <summary>
    /// A session of another kind standing on this record, which inherits what the
    /// record proved and ends when the record does.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="type">Which kind.</param>
    /// <param name="origin">Where it began.</param>
    /// <param name="at">When it began.</param>
    /// <param name="inactivity">How long it survives without use.</param>
    /// <returns>The derived session.</returns>
    /// <exception cref="ArgumentNullException">The origin is absent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The kind is the record's own, which is created by an authentication and never
    /// derived.
    /// </exception>
    public Session Derive(
        SessionId id,
        SessionType type,
        SessionOrigin origin,
        DateTimeOffset at,
        TimeSpan inactivity)
    {
        ArgumentNullException.ThrowIfNull(origin);

        if (type is SessionType.Auth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "The record is created by an authentication and never derived from another.");
        }

        // The derived session is a handle on the record, not a credential of its own:
        // it ends when the record does, whatever its own idle clock says
        // (AUTH-SESS-012 AC7, AUTH-OIDC-003).
        return new Session(
            id,
            Spine,
            type,
            Subject,
            new Assurance(Attained, PhishingResistant),
            origin,
            at,
            inactivity,
            AbsoluteExpiry,
            SatisfiesEveryGate);
    }

    /// <summary>
    /// The session was used, which refreshes what lapses without use and records
    /// where from.
    /// </summary>
    /// <param name="origin">Where it was used.</param>
    /// <param name="at">When.</param>
    /// <param name="inactivity">How long it survives without use.</param>
    /// <exception cref="ArgumentNullException">The origin is absent.</exception>
    public void Touch(SessionOrigin origin, DateTimeOffset at, TimeSpan inactivity)
    {
        ArgumentNullException.ThrowIfNull(origin);

        LastSeen = origin;
        LastSeenAt = at;
        IdleExpiry = at + inactivity;
    }

    /// <summary>
    /// A combination was presented on the session, which writes exactly what it
    /// reached and never more.
    /// </summary>
    /// <param name="reached">What was presented.</param>
    /// <param name="at">When.</param>
    public void Present(Assurance reached, DateTimeOffset at)
    {
        if (reached.Level > Attained)
        {
            Attained = reached.Level;
        }

        AttainedAt = at;

        if (reached.PhishingResistant)
        {
            PhishingResistant = true;
            PhishingResistantAt = at;
        }
    }

    /// <summary>
    /// The session ended, by logout, by revocation, or because the account left
    /// <c>active</c>.
    /// </summary>
    /// <param name="at">When it ended.</param>
    public void End(DateTimeOffset at) => EndedAt ??= at;
}
