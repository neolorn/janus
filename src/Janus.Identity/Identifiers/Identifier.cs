using System;
using Janus.Core;

namespace Janus.Identity.Identifiers;

/// <summary>
/// One identifier of an account: an email address, a telephone number or a username.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-001, REG-IDENT-002, REG-IDENT-010, IDN-ACCT-004 and
/// IDN-ACCT-006. Two forms are kept: the one the person entered, which is what is shown
/// back to them, and the canonical one, which is what is fingerprinted, looked up and
/// compared. An identifier counts for nothing until it is verified. The personal email
/// an invitation named beside a corporate address is kept through the membership that
/// invitation attached (REG-MAIL-001).
/// </remarks>
internal sealed class Identifier
{
    private Identifier(
        IdentifierId id,
        SubjectId subject,
        IdentifierKind kind,
        string entered,
        string canonical,
        DateTimeOffset addedAt,
        bool isLocked)
    {
        Id = id;
        Subject = subject;
        Kind = kind;
        Entered = entered;
        Canonical = canonical;
        AddedAt = addedAt;
        IsLocked = isLocked;
    }

    /// <summary>
    /// The identifier's own identifier, as the identifier endpoints name it.
    /// </summary>
    public IdentifierId Id { get; }

    /// <summary>
    /// Whose identifier it is.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// Which of the three kinds it is.
    /// </summary>
    public IdentifierKind Kind { get; }

    /// <summary>
    /// The form the person entered. This is what is shown back to them, never the
    /// canonical form.
    /// </summary>
    public string Entered { get; private set; }

    /// <summary>
    /// The form it is fingerprinted, looked up and compared under.
    /// </summary>
    public string Canonical { get; private set; }

    /// <summary>
    /// When it was added to the account.
    /// </summary>
    public DateTimeOffset AddedAt { get; }

    /// <summary>
    /// When it was verified, where it has been.
    /// </summary>
    public DateTimeOffset? VerifiedAt { get; private set; }

    /// <summary>
    /// Whether it is the primary of its kind. Ordinary communications go to the
    /// primary; security notices go to the security-notice set.
    /// </summary>
    public bool IsPrimary { get; private set; }

    /// <summary>
    /// Whether it may be changed during registration. A bound invitation and a mailbox
    /// the provider itself operates both leave nothing for the person to change.
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>
    /// Whether it is the personal email an invitation into an organization whose mail
    /// is integrated named beside the corporate address. While the membership lasts it
    /// stays verified and non-primary, and every security notice reaches it whatever
    /// the backup setting (REG-MAIL-001).
    /// </summary>
    public bool IsPersonal { get; private set; }

    /// <summary>
    /// Whether it counts. An unverified identifier signs nobody in and receives no
    /// recovery link.
    /// </summary>
    public bool IsVerified => VerifiedAt is not null;

    /// <summary>
    /// Records an email address a person has given.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="address">The address in its canonical form.</param>
    /// <param name="entered">The address as the person entered it.</param>
    /// <param name="addedAt">When it was added.</param>
    /// <param name="isLocked">Whether it is locked against change.</param>
    /// <returns>The identifier, unverified.</returns>
    /// <exception cref="ArgumentNullException">The entered form is absent.</exception>
    public static Identifier Email(
        IdentifierId id,
        SubjectId subject,
        EmailAddress address,
        string entered,
        DateTimeOffset addedAt,
        bool isLocked = false)
    {
        ArgumentNullException.ThrowIfNull(entered);

        return new Identifier(id, subject, IdentifierKind.Email, entered, address.Value, addedAt, isLocked);
    }

    /// <summary>
    /// Records a telephone number a person has given.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="number">The number in E.164.</param>
    /// <param name="entered">The number as the person entered it.</param>
    /// <param name="addedAt">When it was added.</param>
    /// <returns>The identifier, unverified.</returns>
    /// <exception cref="ArgumentNullException">The entered form is absent.</exception>
    public static Identifier Phone(
        IdentifierId id,
        SubjectId subject,
        PhoneNumber number,
        string entered,
        DateTimeOffset addedAt)
    {
        ArgumentNullException.ThrowIfNull(entered);

        return new Identifier(id, subject, IdentifierKind.Phone, entered, number.Value, addedAt, isLocked: false);
    }

    /// <summary>
    /// Records a username a person has chosen. A username needs no verification: it
    /// reaches nobody, so there is nothing to prove.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="username">The username in the profile's form.</param>
    /// <param name="chosenAt">When it was chosen.</param>
    /// <returns>The identifier, already counting.</returns>
    public static Identifier Username(
        IdentifierId id,
        SubjectId subject,
        Username username,
        DateTimeOffset chosenAt)
    {
        var chosen = new Identifier(
            id,
            subject,
            IdentifierKind.Username,
            username.Value,
            username.Value,
            chosenAt,
            isLocked: false);

        chosen.Verify(chosenAt);

        return chosen;
    }

    /// <summary>
    /// The identifier as it already stands. This is the store translating a stored row
    /// and no operation, so it takes the verification and the primary role it is given
    /// without asking how the identifier came by them.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="kind">Which of the three kinds it is.</param>
    /// <param name="entered">The form the person entered.</param>
    /// <param name="canonical">The form it is compared under.</param>
    /// <param name="addedAt">When it was added.</param>
    /// <param name="verifiedAt">When it was verified, where it has been.</param>
    /// <param name="isPrimary">Whether it is the primary of its kind.</param>
    /// <param name="isLocked">Whether it is locked against change.</param>
    /// <param name="isPersonal">Whether it is the personal email a membership keeps.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ArgumentNullException">Either form is absent.</exception>
    public static Identifier Existing(
        IdentifierId id,
        SubjectId subject,
        IdentifierKind kind,
        string entered,
        string canonical,
        DateTimeOffset addedAt,
        DateTimeOffset? verifiedAt,
        bool isPrimary,
        bool isLocked,
        bool isPersonal)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);

        return new Identifier(id, subject, kind, entered, canonical, addedAt, isLocked)
        {
            VerifiedAt = verifiedAt,
            IsPrimary = isPrimary,
            IsPersonal = isPersonal,
        };
    }

    /// <summary>
    /// Records that a code or a same-browser link confirmed the identifier.
    /// </summary>
    /// <param name="at">When it was confirmed.</param>
    /// <exception cref="InvalidOperationException">It is verified already.</exception>
    public void Verify(DateTimeOffset at)
    {
        if (IsVerified)
        {
            throw new InvalidOperationException("The identifier is verified already.");
        }

        VerifiedAt = at;
    }

    /// <summary>
    /// Replaces the value during registration, which discards the verification the old
    /// value carried.
    /// </summary>
    /// <param name="entered">The new value as the person entered it.</param>
    /// <param name="canonical">The new value in its canonical form.</param>
    /// <exception cref="ArgumentNullException">Either form is absent.</exception>
    /// <exception cref="InvalidOperationException">The identifier is locked.</exception>
    public void Change(string entered, string canonical)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);

        if (IsLocked)
        {
            throw new InvalidOperationException("A locked identifier is not changed.");
        }

        Entered = entered;
        Canonical = canonical;
        VerifiedAt = null;
        IsPrimary = false;
    }

    /// <summary>
    /// Puts a new value in the place of the old one on the same identifier, proved at
    /// the instant given. This is the swap of single-address mode and the change of a
    /// username: the identifier keeps its identity and the role it holds, so nothing
    /// that names it has to be told a new one.
    /// </summary>
    /// <param name="entered">The new value as the person entered it.</param>
    /// <param name="canonical">The new value in its canonical form.</param>
    /// <param name="at">When the new value was proved.</param>
    /// <exception cref="ArgumentNullException">Either form is absent.</exception>
    /// <exception cref="InvalidOperationException">
    /// The identifier is locked, or it is the personal email a membership keeps.
    /// </exception>
    public void Replace(string entered, string canonical, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(canonical);

        if (IsLocked || IsPersonal)
        {
            throw new InvalidOperationException("A locked identifier is not changed.");
        }

        Entered = entered;
        Canonical = canonical;
        VerifiedAt = at;
    }

    /// <summary>
    /// Makes it the primary of its kind. Only the set it belongs to calls this, because
    /// only the set can see the one it displaces.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// It is not verified, or it is the personal email a membership keeps.
    /// </exception>
    internal void MakePrimary()
    {
        if (!IsVerified)
        {
            throw new InvalidOperationException("An unverified identifier is not made primary.");
        }

        if (IsPersonal)
        {
            throw new InvalidOperationException("The personal email a membership keeps is not made primary.");
        }

        IsPrimary = true;
    }

    /// <summary>
    /// Keeps it as the personal email of a membership. Only the set it belongs to calls
    /// this, because only the set can see that another holds the primary role.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// It is not a verified email, or it is the primary.
    /// </exception>
    internal void KeepAsPersonal()
    {
        if (Kind is not IdentifierKind.Email || !IsVerified || IsPrimary)
        {
            throw new InvalidOperationException("Only a verified email that is not the primary is kept.");
        }

        IsPersonal = true;
    }

    /// <summary>
    /// Gives up the primary role to another of its kind.
    /// </summary>
    internal void Relinquish() => IsPrimary = false;
}
