using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// What the fingerprint key's rotation reads and writes: every stored fingerprint, with
/// the version of the key it was computed under.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003, PRIV-RIGHT-005c, OPS-MIG-003a and CONV-DESIGN-003, as entry
/// 318 of the decisions pending review settles them. A fingerprint is computed again
/// from the value it was computed from, decrypted in the command's own process under
/// the versions it was handed; the database sees only fingerprints and ciphertext. A
/// fingerprint no value stands behind any more (a username held after an erasure, the
/// reservation of an erased subject's removed identifier) cannot be computed again and
/// keeps its version until it is released.
/// </remarks>
internal interface IFingerprintRotationStore
{
    /// <summary>
    /// Every version some fingerprint still read is under. A neutralised fingerprint, a
    /// hold released and a reservation expired are read no more.
    /// </summary>
    /// <param name="now">The instant a hold or a reservation is read at.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The versions.</returns>
    ValueTask<IReadOnlySet<int>> FingerprintVersionsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Computes again, under the current version, the fingerprints under another among
    /// the next subjects after the point the ordered pass has reached: their
    /// identifiers, the reservations of the identifiers they removed, and the subjects
    /// their linked providers know them by.
    /// </summary>
    /// <param name="after">The last subject reached, or nothing to start at the first.</param>
    /// <param name="count">How many subjects the batch takes.</param>
    /// <param name="now">The instant a reservation is read at.</param>
    /// <param name="cancellationToken">Abandons the batch.</param>
    /// <returns>The last subject taken, or nothing where none was left, and how many fingerprints were computed again.</returns>
    ValueTask<KeyRotationBatch> RecomputeSubjectsAfterAsync(
        SubjectId? after,
        int count,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Computes again, under the current version, fingerprints still under another
    /// wherever they stand: ones written under a previous version behind the point the
    /// ordered pass had reached, and every mailbox's address.
    /// </summary>
    /// <param name="count">How many fingerprints the batch takes at most.</param>
    /// <param name="now">The instant a reservation is read at.</param>
    /// <param name="cancellationToken">Abandons the batch.</param>
    /// <returns>How many fingerprints were computed again.</returns>
    ValueTask<int> RecomputeRemainingAsync(int count, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// How many fingerprints still read stand under a version other than the current:
    /// the ones nothing can compute again, and any the last batch did not reach.
    /// </summary>
    /// <param name="now">The instant a hold or a reservation is read at.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The count.</returns>
    ValueTask<int> StandingAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets what is under a version other than the current and would be read under
    /// nothing once that version is retired: the lines of the ledgers that count
    /// attempts, sends, notices, registrations and callbacks, and the holds released.
    /// </summary>
    /// <param name="now">The instant a hold is read at.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of forgetting them.</returns>
    ValueTask ForgetAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
