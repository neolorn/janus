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
/// rule that reads a session. It keeps the instant each assurance level was last
/// reached and the instant phishing resistance was last reached, and a presentation
/// renews only what it reaches (D-191). The break-glass session keeps the reason given
/// at the credential's use, set when the credential opens it and never changed; no
/// other session has one (OPS-BOOT-002, D-170).
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
        bool satisfiesEveryGate,
        string? breakGlassReason)
    {
        Id = id;
        Spine = spine;
        Type = type;
        Subject = subject;
        CreatedAt = at;
        LastSeenAt = at;
        Origin = origin;
        LastSeen = origin;
        IdleExpiry = at + inactivity;
        AbsoluteExpiry = absolute;
        SatisfiesEveryGate = satisfiesEveryGate;
        BreakGlassReason = breakGlassReason;
        Present(reached, at);
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

    /// <summary>
    /// The tier the session holds, which is the highest it has reached (AUTH-SESS-001).
    /// </summary>
    public AssuranceLevel Attained =>
        Aal3At is not null
            ? AssuranceLevel.Aal3
            : Aal2At is not null
                ? AssuranceLevel.Aal2
                : Aal1At is not null
                    ? AssuranceLevel.Aal1
                    : AssuranceLevel.Delegated;

    /// <summary>
    /// When <c>delegated</c> or above was last reached, which every authentication
    /// reaches.
    /// </summary>
    public DateTimeOffset DelegatedAt { get; private set; }

    /// <summary>When <c>aal1</c> or above was last reached, or nothing where it never was.</summary>
    public DateTimeOffset? Aal1At { get; private set; }

    /// <summary>When <c>aal2</c> or above was last reached, or nothing where it never was.</summary>
    public DateTimeOffset? Aal2At { get; private set; }

    /// <summary>When <c>aal3</c> was last reached, or nothing where it never was.</summary>
    public DateTimeOffset? Aal3At { get; private set; }

    /// <summary>Whether the session has reached anything resisting credential relay.</summary>
    public bool PhishingResistant => PhishingResistantAt is not null;

    /// <summary>When phishing resistance was last reached, or nothing where it never was.</summary>
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
    /// When it was last downgraded, or nothing where it never was. The instants of the
    /// levels and of phishing resistance stay as they were reached, and a gate counts
    /// one only where it lies after this instant (AUTH-SESS-009, AUTH-STEP-002).
    /// </summary>
    public DateTimeOffset? DowngradedAt { get; private set; }

    /// <summary>
    /// Whether the session passes every gate and the stated floor for its lifetime,
    /// which only the emergency path sets and nothing else does.
    /// </summary>
    public bool SatisfiesEveryGate { get; }

    /// <summary>
    /// The reason given at the use of the break-glass credential, which only the session
    /// it opened, and a session derived from it, carries.
    /// </summary>
    public string? BreakGlassReason { get; }

    /// <summary>
    /// The client a registration captured, which the session its terms step establishes
    /// carries, and nothing on any other session (REG-SESS-008, API-REDIR-002).
    /// </summary>
    public string? Client { get; private set; }

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
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, which only the
    /// emergency path gives; a session opened with one passes every gate and the stated
    /// floor for its lifetime (AUTH-SESS-005b, AUTH-STEP-004).
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
        string? breakGlassReason)
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
            satisfiesEveryGate: breakGlassReason is not null,
            breakGlassReason);
    }

    /// <summary>
    /// The session as it already stands, which is the store's translation of a row
    /// and no change to it.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="spine">The record it derives from, which is itself for a record.</param>
    /// <param name="type">Which kind.</param>
    /// <param name="subject">Whose session it is.</param>
    /// <param name="createdAt">When it began.</param>
    /// <param name="lastSeenAt">When it was last used.</param>
    /// <param name="delegatedAt">When it last reached <c>delegated</c> or above.</param>
    /// <param name="aal1At">When it last reached <c>aal1</c> or above, or nothing.</param>
    /// <param name="aal2At">When it last reached <c>aal2</c> or above, or nothing.</param>
    /// <param name="aal3At">When it last reached <c>aal3</c>, or nothing.</param>
    /// <param name="phishingResistantAt">When it last reached phishing resistance, or nothing.</param>
    /// <param name="origin">Where it began.</param>
    /// <param name="lastSeen">Where it was last used.</param>
    /// <param name="idleExpiry">When it lapses without use.</param>
    /// <param name="absoluteExpiry">When it lapses whatever happens.</param>
    /// <param name="endedAt">When it ended, or nothing while it stands.</param>
    /// <param name="downgradedAt">When it was last downgraded, or nothing where it never was.</param>
    /// <param name="satisfiesEveryGate">Whether it passes every gate while it lasts.</param>
    /// <param name="breakGlassReason">The reason given at the credential's use, where one was.</param>
    /// <param name="client">The client a registration captured, where it established the session.</param>
    /// <returns>The session.</returns>
    /// <exception cref="ArgumentNullException">An origin is absent.</exception>
    public static Session Existing(
        SessionId id,
        SessionId spine,
        SessionType type,
        SubjectId subject,
        DateTimeOffset createdAt,
        DateTimeOffset lastSeenAt,
        DateTimeOffset delegatedAt,
        DateTimeOffset? aal1At,
        DateTimeOffset? aal2At,
        DateTimeOffset? aal3At,
        DateTimeOffset? phishingResistantAt,
        SessionOrigin origin,
        SessionOrigin lastSeen,
        DateTimeOffset idleExpiry,
        DateTimeOffset absoluteExpiry,
        DateTimeOffset? endedAt,
        DateTimeOffset? downgradedAt,
        bool satisfiesEveryGate,
        string? breakGlassReason,
        string? client)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(lastSeen);

        return new Session(
            id,
            spine,
            type,
            subject,
            new Assurance(AssuranceLevel.Delegated, PhishingResistant: false),
            origin,
            createdAt,
            TimeSpan.Zero,
            absoluteExpiry,
            satisfiesEveryGate,
            breakGlassReason)
        {
            LastSeenAt = lastSeenAt,
            DelegatedAt = delegatedAt,
            Aal1At = aal1At,
            Aal2At = aal2At,
            Aal3At = aal3At,
            PhishingResistantAt = phishingResistantAt,
            LastSeen = lastSeen,
            IdleExpiry = idleExpiry,
            EndedAt = endedAt,
            DowngradedAt = downgradedAt,
            Client = client,
        };
    }

    /// <summary>
    /// Keeps the client a registration captured on the session its terms step
    /// establishes, which is where the done step reads its return from (REG-SESS-008).
    /// </summary>
    /// <param name="client">The client the registration captured.</param>
    /// <exception cref="ArgumentException">The client is blank.</exception>
    public void Capture(string client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(client);

        Client = client;
    }

    /// <summary>
    /// A session of another kind standing on this record, which inherits what the
    /// record proved and ends when the record does. It takes the instant the record
    /// last reached each level and phishing resistance, and the record's last
    /// downgrade, never the instant it is derived at, since deriving presents nothing
    /// (AUTH-SESS-012).
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
        // AUTH-SESS-012 AC8, AUTH-SESS-009: the handle proves what the record proved,
        // when the record proved it, so opening another application renews no proof
        // and lifts no downgrade.
        return new Session(
            id,
            Spine,
            type,
            Subject,
            new Assurance(AssuranceLevel.Delegated, PhishingResistant: false),
            origin,
            at,
            inactivity,
            AbsoluteExpiry,
            SatisfiesEveryGate,
            BreakGlassReason)
        {
            DelegatedAt = DelegatedAt,
            Aal1At = Aal1At,
            Aal2At = Aal2At,
            Aal3At = Aal3At,
            PhishingResistantAt = PhishingResistantAt,
            DowngradedAt = DowngradedAt,
        };
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
    /// A combination was presented on the session, which writes the instant of each
    /// level it reaches, its own and every lower one, and of phishing resistance where
    /// it reaches it, and changes no instant of what it does not reach (AUTH-SESS-001).
    /// </summary>
    /// <param name="reached">What was presented.</param>
    /// <param name="at">When.</param>
    public void Present(Assurance reached, DateTimeOffset at)
    {
        DelegatedAt = at;

        if (reached.Level >= AssuranceLevel.Aal1)
        {
            Aal1At = at;
        }

        if (reached.Level >= AssuranceLevel.Aal2)
        {
            Aal2At = at;
        }

        if (reached.Level >= AssuranceLevel.Aal3)
        {
            Aal3At = at;
        }

        if (reached.PhishingResistant)
        {
            PhishingResistantAt = at;
        }
    }

    /// <summary>
    /// When a level or one above it was last reached. A presentation writes every level
    /// below the one it reaches, so the instant kept for a level is the last instant
    /// that level or a higher one was reached (AUTH-STEP-002 step 1).
    /// </summary>
    /// <param name="level">The level.</param>
    /// <returns>The instant, or nothing where the session never reached the level.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The level is none of chapter 10 section 5.4.</exception>
    public DateTimeOffset? LastReached(AssuranceLevel level) =>
        level switch
        {
            AssuranceLevel.Delegated => DelegatedAt,
            AssuranceLevel.Aal1 => Aal1At,
            AssuranceLevel.Aal2 => Aal2At,
            AssuranceLevel.Aal3 => Aal3At,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "The level is none the catalogue lists."),
        };

    /// <summary>
    /// Downgrades the session: what it reached up to this instant passes no gate until
    /// a combination the policy in force permits is presented, and what that
    /// presentation reaches counts from then (AUTH-SESS-009).
    /// </summary>
    /// <param name="at">When the policy in force tightened.</param>
    public void Downgrade(DateTimeOffset at) => DowngradedAt = at;

    /// <summary>
    /// Whether what was reached at an instant counts at a gate: what was reached after
    /// the session's last downgrade does, and what was reached up to it does not
    /// (AUTH-STEP-002 step 1).
    /// </summary>
    /// <param name="reachedAt">When it was reached.</param>
    /// <returns>Whether a gate counts it.</returns>
    public bool Counts(DateTimeOffset reachedAt) => DowngradedAt is not { } downgraded || reachedAt > downgraded;

    /// <summary>
    /// The session ended, by logout, by revocation, or because the account left
    /// <c>active</c>.
    /// </summary>
    /// <param name="at">When it ended.</param>
    public void End(DateTimeOffset at) => EndedAt ??= at;
}
