using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// A record access session: asked by a code sent to an email or phone, opened by that
/// code in the browser that asked, and naming the person records it reaches.
/// </summary>
/// <remarks>
/// Implements REC-ACC-001. It holds the value's keyed fingerprints and never the value,
/// and lives <c>records.access.lifetime</c> from its opening, never extended. Before it
/// opens it lives as long as the code that opens it.
/// </remarks>
internal sealed class RecordAccessSession
{
    private readonly List<SubjectId> _reached;

    private RecordAccessSession(
        RecordAccessSessionId id,
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        byte[] throttled,
        DateTimeOffset createdAt,
        DateTimeOffset? openedAt,
        DateTimeOffset expiresAt,
        IEnumerable<SubjectId> reached)
    {
        Id = id;
        Kind = kind;
        Fingerprints = fingerprints;
        Throttled = throttled;
        CreatedAt = createdAt;
        OpenedAt = openedAt;
        ExpiresAt = expiresAt;
        _reached = [.. reached];
    }

    /// <summary>
    /// The session's identifier.
    /// </summary>
    public RecordAccessSessionId Id { get; }

    /// <summary>
    /// Whether the value is an email or a phone.
    /// </summary>
    public IdentifierKind Kind { get; }

    /// <summary>
    /// The value's keyed fingerprints, one under each version of the key a record's
    /// contact may be held under.
    /// </summary>
    [NeverLogged]
    public IReadOnlyList<byte[]> Fingerprints { get; }

    /// <summary>
    /// What a wrong code, or a reference that adds nothing, is counted against for the
    /// value (AUTH-ABUSE-001).
    /// </summary>
    [NeverLogged]
    public byte[] Throttled { get; }

    /// <summary>
    /// When the code was asked for.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When the code opened the session, where it has.
    /// </summary>
    public DateTimeOffset? OpenedAt { get; private set; }

    /// <summary>
    /// When it stops answering.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the code has opened it.
    /// </summary>
    public bool IsOpen => OpenedAt is not null;

    /// <summary>
    /// The subjects of the records it reached, at its opening or by a reference.
    /// </summary>
    public IReadOnlyList<SubjectId> Reached => _reached;

    /// <summary>
    /// A session asked for, the code that opens it outstanding.
    /// </summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="kind">Whether the value is an email or a phone.</param>
    /// <param name="fingerprints">The value's keyed fingerprints.</param>
    /// <param name="throttled">What the value's tries are counted against.</param>
    /// <param name="at">When the code was asked for.</param>
    /// <param name="codeLifetime">How long the code lives.</param>
    /// <returns>The session.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static RecordAccessSession Ask(
        RecordAccessSessionId id,
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        byte[] throttled,
        DateTimeOffset at,
        TimeSpan codeLifetime)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);
        ArgumentNullException.ThrowIfNull(throttled);

        return new RecordAccessSession(id, kind, fingerprints, throttled, at, openedAt: null, at + codeLifetime, []);
    }

    /// <summary>
    /// The session as it already stands, which is the store translating a row and no
    /// step the person took.
    /// </summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="kind">Whether the value is an email or a phone.</param>
    /// <param name="fingerprints">The value's keyed fingerprints.</param>
    /// <param name="throttled">What the value's tries are counted against.</param>
    /// <param name="createdAt">When the code was asked for.</param>
    /// <param name="openedAt">When the code opened it, where it has.</param>
    /// <param name="expiresAt">When it stops answering.</param>
    /// <param name="reached">The subjects of the records it reached.</param>
    /// <returns>The session.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static RecordAccessSession Existing(
        RecordAccessSessionId id,
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        byte[] throttled,
        DateTimeOffset createdAt,
        DateTimeOffset? openedAt,
        DateTimeOffset expiresAt,
        IEnumerable<SubjectId> reached)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);
        ArgumentNullException.ThrowIfNull(throttled);
        ArgumentNullException.ThrowIfNull(reached);

        return new RecordAccessSession(id, kind, fingerprints, throttled, createdAt, openedAt, expiresAt, reached);
    }

    /// <summary>
    /// Whether it has stopped answering.
    /// </summary>
    /// <param name="now">The instant to judge at.</param>
    /// <returns>Whether it has.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Opens the session by its code, for a lifetime from now that nothing extends.
    /// </summary>
    /// <param name="at">When the code was presented.</param>
    /// <param name="lifetime">How long the session lives from its opening.</param>
    /// <exception cref="InvalidOperationException">It is open already.</exception>
    public void Open(DateTimeOffset at, TimeSpan lifetime)
    {
        if (IsOpen)
        {
            throw new InvalidOperationException("A record access session is opened once.");
        }

        OpenedAt = at;
        ExpiresAt = at + lifetime;
    }

    /// <summary>
    /// Adds the records the session now reaches.
    /// </summary>
    /// <param name="subjects">Their subjects.</param>
    /// <returns>Those it did not reach before.</returns>
    /// <exception cref="ArgumentNullException">The subjects are absent.</exception>
    public IReadOnlyList<SubjectId> Reach(IEnumerable<SubjectId> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        SubjectId[] added = [.. subjects.Distinct().Where(subject => !_reached.Contains(subject))];

        _reached.AddRange(added);

        return added;
    }
}
