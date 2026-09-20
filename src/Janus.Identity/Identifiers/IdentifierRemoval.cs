using System;
using Janus.Core;

namespace Janus.Identity.Identifiers;

/// <summary>
/// An identifier the account gave up, held for as long as the undo link the remaining
/// channels were sent is good for.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-006. The removal takes effect at once: the value resolves to
/// nobody from the moment it is recorded here, so presenting it at a recovery path
/// behaves as an unknown identifier. What the record keeps is enough to put it back
/// unchanged, and the value stays out of another account's reach until the window
/// elapses, so the undo cannot be beaten to it.
/// </remarks>
internal sealed class IdentifierRemoval
{
    private IdentifierRemoval(
        IdentifierId id,
        SubjectId subject,
        IdentifierKind kind,
        string entered,
        string canonical,
        bool isLocked,
        DateTimeOffset addedAt,
        DateTimeOffset verifiedAt,
        DateTimeOffset removedAt,
        DateTimeOffset expiresAt,
        byte[] undo)
    {
        Id = id;
        Subject = subject;
        Kind = kind;
        Entered = entered;
        Canonical = canonical;
        IsLocked = isLocked;
        AddedAt = addedAt;
        VerifiedAt = verifiedAt;
        RemovedAt = removedAt;
        ExpiresAt = expiresAt;
        Undo = undo;
    }

    /// <summary>
    /// The identifier it was, which the restored one is again.
    /// </summary>
    public IdentifierId Id { get; }

    /// <summary>
    /// Whose it was.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// Which of the three kinds it is.
    /// </summary>
    public IdentifierKind Kind { get; }

    /// <summary>
    /// The form the person entered.
    /// </summary>
    public string Entered { get; }

    /// <summary>
    /// The form it is fingerprinted and compared under.
    /// </summary>
    public string Canonical { get; }

    /// <summary>
    /// Whether it was locked against change.
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>
    /// When the account first took it on.
    /// </summary>
    public DateTimeOffset AddedAt { get; }

    /// <summary>
    /// When it was verified, which the restored identifier carries unchanged.
    /// </summary>
    public DateTimeOffset VerifiedAt { get; }

    /// <summary>
    /// When the account gave it up.
    /// </summary>
    public DateTimeOffset RemovedAt { get; }

    /// <summary>
    /// When the undo stops working and the value is released.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// The fingerprint of the token the undo link carries.
    /// </summary>
    public byte[] Undo { get; }

    /// <summary>
    /// Records that an account gave an identifier up.
    /// </summary>
    /// <param name="identifier">The identifier as it stood.</param>
    /// <param name="at">When it was given up.</param>
    /// <param name="expiresAt">When the undo stops working.</param>
    /// <param name="undo">The fingerprint of the undo link's token.</param>
    /// <returns>The removal.</returns>
    /// <exception cref="ArgumentNullException">The identifier or the fingerprint is absent.</exception>
    /// <exception cref="ArgumentException">The identifier was never verified.</exception>
    public static IdentifierRemoval Of(
        Identifier identifier,
        DateTimeOffset at,
        DateTimeOffset expiresAt,
        byte[] undo)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(undo);

        if (identifier.VerifiedAt is not DateTimeOffset verified)
        {
            throw new ArgumentException(
                "An identifier that was never verified is discarded, not removed.",
                nameof(identifier));
        }

        return new IdentifierRemoval(
            identifier.Id,
            identifier.Subject,
            identifier.Kind,
            identifier.Entered,
            identifier.Canonical,
            identifier.IsLocked,
            identifier.AddedAt,
            verified,
            at,
            expiresAt,
            undo);
    }

    /// <summary>
    /// The removal as it already stands. This is the store's translation of a stored
    /// row and no removal the account made.
    /// </summary>
    /// <param name="id">The identifier it was.</param>
    /// <param name="subject">Whose it was.</param>
    /// <param name="kind">Which of the three kinds it is.</param>
    /// <param name="entered">The form the person entered.</param>
    /// <param name="canonical">The form it is compared under.</param>
    /// <param name="isLocked">Whether it was locked against change.</param>
    /// <param name="addedAt">When the account took it on.</param>
    /// <param name="verifiedAt">When it was verified.</param>
    /// <param name="removedAt">When the account gave it up.</param>
    /// <param name="expiresAt">When the undo stops working.</param>
    /// <param name="undo">The fingerprint of the undo link's token.</param>
    /// <returns>The removal.</returns>
    /// <exception cref="ArgumentNullException">A form or the fingerprint is absent.</exception>
    public static IdentifierRemoval Existing(
        IdentifierId id,
        SubjectId subject,
        IdentifierKind kind,
        string entered,
        string canonical,
        bool isLocked,
        DateTimeOffset addedAt,
        DateTimeOffset verifiedAt,
        DateTimeOffset removedAt,
        DateTimeOffset expiresAt,
        byte[] undo)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);
        ArgumentNullException.ThrowIfNull(undo);

        return new IdentifierRemoval(
            id,
            subject,
            kind,
            entered,
            canonical,
            isLocked,
            addedAt,
            verifiedAt,
            removedAt,
            expiresAt,
            undo);
    }

    /// <summary>
    /// Whether the undo window has run out at an instant.
    /// </summary>
    /// <param name="now">The instant.</param>
    /// <returns>Whether the undo is too late.</returns>
    public bool HasElapsed(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// The identifier as it was, for the set to take back. It returns unverified of
    /// nothing and primary of nothing: the role is the set's to settle.
    /// </summary>
    /// <returns>The identifier.</returns>
    public Identifier Restored() =>
        Identifier.Existing(
            Id,
            Subject,
            Kind,
            Entered,
            Canonical,
            AddedAt,
            VerifiedAt,
            isPrimary: false,
            IsLocked);
}
