using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Identifiers;

/// <summary>
/// What the identifier operations ask of the account directory.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-002 to REG-IDENT-009 and CONV-LAYOUT-001. Every rule that
/// holds across an account's identifiers lives on the other side of this port; what
/// asks here has already decided that the operation is allowed, so a command that
/// arrives is carried out.
/// </remarks>
internal interface IIdentifierDirectory
{
    /// <summary>
    /// Finds the account an identifier belongs to.
    /// </summary>
    /// <param name="kind">Which kind the value is.</param>
    /// <param name="canonical">The value in its canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account holding it, or nothing where no account holds it.</returns>
    ValueTask<SubjectId?> OwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds the account an identifier belongs to and the identifier itself.
    /// </summary>
    /// <param name="kind">Which kind the value is.</param>
    /// <param name="canonical">The value in its canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account and the identifier, or nothing where no account holds it.</returns>
    ValueTask<(SubjectId Subject, IdentifierId Identifier)?> HolderAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a value is held out of reach by a removal whose undo has not run out.
    /// </summary>
    /// <param name="kind">Which kind the value is.</param>
    /// <param name="canonical">The value in its canonical form.</param>
    /// <param name="now">The instant the window is judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the value is out of reach.</returns>
    ValueTask<bool> IsReservedAsync(
        IdentifierKind kind,
        string canonical,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a username is held after the erasure of the account that bore it.
    /// </summary>
    /// <param name="canonical">The username in its canonical form.</param>
    /// <param name="now">The instant the hold is judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it is still held.</returns>
    ValueTask<bool> IsHeldAsync(
        string canonical,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// One account's identifiers as they stand.
    /// </summary>
    /// <param name="subject">Whose identifiers.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The identifiers, the backup settings and the security-notice set.</returns>
    ValueTask<HeldIdentifiers> HeldAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Takes an identifier on to the account, unverified.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="kind">Which kind it is.</param>
    /// <param name="entered">The form the person entered.</param>
    /// <param name="canonical">The form it is compared under.</param>
    /// <param name="at">When it was added.</param>
    /// <param name="maximum">How many of its kind the account may hold.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking it on.</returns>
    ValueTask TakeOnAsync(
        SubjectId subject,
        IdentifierId id,
        IdentifierKind kind,
        string entered,
        string canonical,
        DateTimeOffset at,
        int maximum,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes a username on to the account, which counts from the instant it is chosen.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="username">The username in the profile's form.</param>
    /// <param name="at">When it was chosen.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking it on.</returns>
    ValueTask TakeUsernameAsync(
        SubjectId subject,
        IdentifierId id,
        Username username,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes on the corporate address an organization asserts, verified, locked and
    /// primary, and keeps the personal email it displaces through the membership
    /// (REG-MAIL-001).
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="id">The identifier issued for the corporate address.</param>
    /// <param name="entered">The address as the administrator entered it.</param>
    /// <param name="canonical">The address in its canonical form.</param>
    /// <param name="personal">The verified personal email the membership keeps.</param>
    /// <param name="at">When the membership attached.</param>
    /// <param name="maximum">How many emails the account may hold.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking it on.</returns>
    ValueTask TakeCorporateAsync(
        SubjectId subject,
        IdentifierId id,
        string entered,
        string canonical,
        IdentifierId personal,
        DateTimeOffset at,
        int maximum,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a code or a same-browser link proved an identifier.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="id">Which identifier.</param>
    /// <param name="at">When it was proved.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask ProveAsync(
        SubjectId subject,
        IdentifierId id,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Puts a new value in the place of the old one on the same identifier.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="id">Which identifier.</param>
    /// <param name="entered">The new value as the person entered it.</param>
    /// <param name="canonical">The new value in its canonical form.</param>
    /// <param name="at">When the new value was proved.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of swapping it.</returns>
    ValueTask SwapAsync(
        SubjectId subject,
        IdentifierId id,
        string entered,
        string canonical,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Puts a new value in the place of the old one on the same identifier and holds
    /// the displaced value for as long as the undo is good for. This is the replace of
    /// single-address mode, where the row keeps its identity and its role and only the
    /// value moves.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="id">Which identifier.</param>
    /// <param name="entered">The new value as the person entered it.</param>
    /// <param name="canonical">The new value in its canonical form.</param>
    /// <param name="at">When the new value was proved.</param>
    /// <param name="expiresAt">When the undo stops working.</param>
    /// <param name="undo">The fingerprint of the undo link's token.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of replacing it.</returns>
    ValueTask ReplaceAsync(
        SubjectId subject,
        IdentifierId id,
        string entered,
        string canonical,
        DateTimeOffset at,
        DateTimeOffset expiresAt,
        byte[] undo,
        CancellationToken cancellationToken);

    /// <summary>
    /// Makes a verified identifier the primary of its kind.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="id">Which identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of promoting it.</returns>
    ValueTask PromoteAsync(SubjectId subject, IdentifierId id, CancellationToken cancellationToken);

    /// <summary>
    /// Settles what a kind's security-notice set holds beyond the primary.
    /// </summary>
    /// <param name="subject">Whose setting.</param>
    /// <param name="kind">The kind it governs.</param>
    /// <param name="choice">What it adds to the primary.</param>
    /// <param name="named">The identifier it names, where it names one.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of settling it.</returns>
    ValueTask SettleBackupAsync(
        SubjectId subject,
        IdentifierKind kind,
        BackupChoice choice,
        IdentifierId? named,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes an unverified identifier off the account, which leaves nothing behind:
    /// a value nobody proved holds nothing out of reach and has no undo.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="id">Which identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of discarding it.</returns>
    ValueTask DiscardAsync(SubjectId subject, IdentifierId id, CancellationToken cancellationToken);

    /// <summary>
    /// The language the account settled on, where it settled one.
    /// </summary>
    /// <param name="subject">Whose language.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The BCP 47 tag, or nothing where the account has none.</returns>
    ValueTask<string?> LanguageAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Takes an identifier off the account and holds its value for as long as the undo
    /// is good for.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="id">Which identifier.</param>
    /// <param name="at">When it was given up.</param>
    /// <param name="expiresAt">When the undo stops working.</param>
    /// <param name="undo">The fingerprint of the undo link's token.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking it off.</returns>
    ValueTask GiveUpAsync(
        SubjectId subject,
        IdentifierId id,
        DateTimeOffset at,
        DateTimeOffset expiresAt,
        byte[] undo,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds the identifier an undo link answers to.
    /// </summary>
    /// <param name="undo">The fingerprint of the token the link carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What was given up, or nothing where no removal answers to it.</returns>
    ValueTask<GivenUpIdentifier?> GivenUpAsync(byte[] undo, CancellationToken cancellationToken);

    /// <summary>
    /// Puts a removed identifier back, verified exactly as it was.
    /// </summary>
    /// <param name="id">Which identifier.</param>
    /// <param name="maximum">How many of its kind the account may hold.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of putting it back.</returns>
    ValueTask TakeBackAsync(IdentifierId id, int maximum, CancellationToken cancellationToken);
}
