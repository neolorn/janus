using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Identity.Identifiers;

/// <summary>
/// Where an account's identifiers are read and written.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004, REG-IDENT-002, PRIV-RIGHT-005a, PRIV-RIGHT-005c and
/// CONV-DESIGN-003. A value crosses this boundary in the two forms the account holds it
/// in; which of them is encrypted, which is fingerprinted and which is looked up is the
/// implementation's business.
/// </remarks>
internal interface IIdentifierStore
{
    /// <summary>
    /// Reads one account's identifiers and the backup settings it has changed.
    /// </summary>
    /// <param name="subject">Whose identifiers to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The set, empty where the account holds none.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The subject's key has been erased, so the identifiers it holds are unreadable.
    /// </exception>
    ValueTask<IdentifierSet> FindBySubjectAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the account an identifier belongs to. An identifier belongs to at most one
    /// account, and one whose fingerprint erasure neutralised belongs to none.
    /// </summary>
    /// <param name="kind">Which kind the value is.</param>
    /// <param name="canonical">The value in its canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account holding it, or nothing where no account holds it.</returns>
    ValueTask<SubjectId?> FindOwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a value is held out of reach by a removal whose undo has not run out
    /// (REG-IDENT-006). A reserved value belongs to no account and resolves to none,
    /// so this is asked only where something is about to be taken on.
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
    /// Finds the removal an undo link answers to.
    /// </summary>
    /// <param name="fingerprint">The fingerprint of the token the link carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The removal, or nothing where no removal answers to it.</returns>
    ValueTask<IdentifierRemoval?> FindRemovalAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds the removal of one identifier, which is what an undo reads once the link
    /// has named it.
    /// </summary>
    /// <param name="id">Which identifier's removal.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The removal, or nothing where the identifier was not given up.</returns>
    ValueTask<IdentifierRemoval?> FindRemovalAsync(
        IdentifierId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that an account gave an identifier up, which holds the value out of
    /// reach for as long as the undo is good for.
    /// </summary>
    /// <param name="removal">What was given up.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordRemovalAsync(IdentifierRemoval removal, CancellationToken cancellationToken);

    /// <summary>
    /// Gives a removal record up, which is what an undo does once the identifier is
    /// back on the account.
    /// </summary>
    /// <param name="id">Which identifier's removal.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of giving it up.</returns>
    ValueTask DiscardRemovalAsync(IdentifierId id, CancellationToken cancellationToken);

    /// <summary>
    /// Releases every value whose undo window has run out.
    /// </summary>
    /// <param name="now">The instant the windows are judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were released.</returns>
    ValueTask<int> SweepRemovalsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a username is held after the erasure of the account that bore it
    /// (REG-IDENT-009).
    /// </summary>
    /// <param name="canonical">The username in its canonical form.</param>
    /// <param name="now">The instant the hold is judged at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it is still held.</returns>
    ValueTask<bool> IsHeldAsync(string canonical, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the set as it now stands onto the rows: identifiers the account has
    /// taken on or given up, the verifications and primary roles it has moved, and the
    /// backup settings it has changed or returned to the default.
    /// </summary>
    /// <param name="set">The account's identifiers as they now stand.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording them.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The subject's key has been erased, so nothing more is written under it.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">The subject has no key.</exception>
    ValueTask RecordAsync(IdentifierSet set, CancellationToken cancellationToken);
}
