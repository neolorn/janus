using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// The offer to attach an account's session holds after a proof: the values proved and
/// the records the proof counted, for <c>records.access.lifetime</c> from the proof.
/// </summary>
/// <remarks>
/// Implements REC-ATT-001. It names the records it counted by their subjects alone, and
/// the attachment judges each again under its lock, so a record that changed meanwhile
/// is simply not attached.
/// </remarks>
internal sealed class OfferToAttach
{
    private OfferToAttach(
        SessionId session,
        IReadOnlyList<ProvenValue> proven,
        IReadOnlyList<SubjectId> offered,
        DateTimeOffset expiresAt)
    {
        Session = session;
        Proven = proven;
        Offered = offered;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// The session the offer is held on.
    /// </summary>
    public SessionId Session { get; }

    /// <summary>
    /// The values the proof proved, which an attachment by a reference reads.
    /// </summary>
    public IReadOnlyList<ProvenValue> Proven { get; }

    /// <summary>
    /// The records the proof counted, in the order of their subjects.
    /// </summary>
    public IReadOnlyList<SubjectId> Offered { get; }

    /// <summary>
    /// When the offer ends.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// The offer as the account reads it: a count and its expiry.
    /// </summary>
    public RecordOffer Shown => new(Offered.Count, ExpiresAt);

    /// <summary>
    /// Makes the offer a proof brings.
    /// </summary>
    /// <param name="session">The session it is held on.</param>
    /// <param name="proven">The values proved.</param>
    /// <param name="offered">The records counted.</param>
    /// <param name="at">The instant of the proof.</param>
    /// <param name="lifetime">The length of <c>records.access.lifetime</c>.</param>
    /// <returns>The offer.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static OfferToAttach Made(
        SessionId session,
        IReadOnlyList<ProvenValue> proven,
        IEnumerable<SubjectId> offered,
        DateTimeOffset at,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(proven);
        ArgumentNullException.ThrowIfNull(offered);

        return new OfferToAttach(
            session,
            [.. proven],
            [.. offered.Distinct().OrderBy(subject => subject.Value)],
            at + lifetime);
    }

    /// <summary>
    /// An offer as it was stored.
    /// </summary>
    /// <param name="session">The session it is held on.</param>
    /// <param name="proven">The values proved.</param>
    /// <param name="offered">The records counted.</param>
    /// <param name="expiresAt">When it ends.</param>
    /// <returns>The offer.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static OfferToAttach Existing(
        SessionId session,
        IReadOnlyList<ProvenValue> proven,
        IReadOnlyList<SubjectId> offered,
        DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(proven);
        ArgumentNullException.ThrowIfNull(offered);

        return new OfferToAttach(session, proven, offered, expiresAt);
    }

    /// <summary>
    /// Whether the offer's life has run out.
    /// </summary>
    /// <param name="now">The instant asked about.</param>
    /// <returns>Whether it has.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;
}
