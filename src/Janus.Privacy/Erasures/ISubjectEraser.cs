using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// The erasure itself: one operation that leaves a subject's personal fields
/// unrecoverable and every row where it was.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-005, PRIV-RIGHT-005a, PRIV-RIGHT-005c, IDN-LIFE-003b,
/// IDN-LIFE-014, IDN-ACCT-002 and CONV-DESIGN-003. It is one port rather than a call
/// per table because IDN-LIFE-003b AC4 admits exactly two outcomes: no row at all, or
/// a row whose key destruction and fingerprint neutralisation committed with it.
/// </remarks>
internal interface ISubjectEraser
{
    /// <summary>
    /// Erases a subject: the account reaches <c>deleted</c>, its wrapped key is
    /// overwritten with an irreversible value, its fingerprints are neutralised, its
    /// photo is removed, and the erasure is recorded.
    /// </summary>
    /// <param name="subject">Whose fields to erase.</param>
    /// <param name="reason">Why the erasure is happening.</param>
    /// <param name="at">The instant of the erasure.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The erasure, with the host-side work outstanding.</returns>
    /// <exception cref="InvalidOperationException">
    /// The subject has no account, has no key, or has already been erased.
    /// </exception>
    ValueTask<Erasure> EraseAsync(
        SubjectId subject,
        ErasureReason reason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Erases a subject again after a restore to a point before its erasure: the same
    /// writes as <see cref="EraseAsync"/>, from whatever state the restore left the
    /// account in (DR-016).
    /// </summary>
    /// <param name="subject">Whose fields to erase.</param>
    /// <param name="reason">Why the erasure happened, as the ledger records it.</param>
    /// <param name="by">The origin the deletion is recorded under where the account was not deleting.</param>
    /// <param name="at">The instant of the erasure, as the ledger records it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The erasure, with the host-side work outstanding.</returns>
    /// <exception cref="InvalidOperationException">
    /// The subject has no account, has no key, or has already been erased.
    /// </exception>
    ValueTask<Erasure> ReapplyAsync(
        SubjectId subject,
        ErasureReason reason,
        DeletionOrigin by,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
