using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// What the key-encryption key's rotation reads and writes: the progress of every
/// rotation, which the fingerprint key's shares, and every value held wrapped under a
/// version of the key.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003, OPS-MIG-003a and CONV-DESIGN-003. Every value is unwrapped
/// and wrapped again in the command's own process, under the versions it was handed;
/// the database sees only what is wrapped.
/// </remarks>
internal interface IKeyRotationStore
{
    /// <summary>
    /// Whether the connection runs under the maintenance credential: with the rights of
    /// the maintenance role, and with no path to the application's.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Whether it does.</returns>
    ValueTask<bool> UnderMaintenanceCredentialAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The rotation of the kind to the highest version, where one was ever started.
    /// </summary>
    /// <param name="kind">Which key.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The rotation, or nothing.</returns>
    ValueTask<KeyRotationProgress?> LatestAsync(KeyRotationKind kind, CancellationToken cancellationToken);

    /// <summary>
    /// Records a rotation starting.
    /// </summary>
    /// <param name="progress">The rotation.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask AddAsync(KeyRotationProgress progress, CancellationToken cancellationToken);

    /// <summary>
    /// Records how far a rotation has gone.
    /// </summary>
    /// <param name="progress">The rotation.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask RecordAsync(KeyRotationProgress progress, CancellationToken cancellationToken);

    /// <summary>
    /// Every version some stored value is wrapped under. An erased subject key is
    /// wrapped under none.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The versions.</returns>
    ValueTask<IReadOnlySet<int>> WrappingVersionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Re-wraps, under the current version, the keys among the next subjects after the
    /// point the ordered pass has reached that are under another.
    /// </summary>
    /// <param name="after">The last subject reached, or nothing to start at the first.</param>
    /// <param name="count">How many subjects the batch takes.</param>
    /// <param name="cancellationToken">Abandons the batch.</param>
    /// <returns>The last subject taken, or nothing where none was left, and how many keys were re-wrapped.</returns>
    ValueTask<KeyRotationBatch> ReWrapSubjectKeysAfterAsync(
        SubjectId? after,
        int count,
        CancellationToken cancellationToken);

    /// <summary>
    /// Re-wraps, under the current version, subject keys still under another wherever
    /// they stand in the order: ones written under a previous version behind the point
    /// the ordered pass had reached.
    /// </summary>
    /// <param name="count">How many keys the batch takes at most.</param>
    /// <param name="cancellationToken">Abandons the batch.</param>
    /// <returns>How many keys were re-wrapped.</returns>
    ValueTask<int> ReWrapRemainingSubjectKeysAsync(int count, CancellationToken cancellationToken);

    /// <summary>
    /// Re-wraps, under the current version, the values held wrapped under another beside
    /// the subject keys: an invitation's, a reserved mailbox's, a registration's and a
    /// queued message's own data key, a sign-on proof, and a token signing key.
    /// </summary>
    /// <param name="count">How many values the batch takes at most.</param>
    /// <param name="cancellationToken">Abandons the batch.</param>
    /// <returns>How many values were re-wrapped.</returns>
    ValueTask<int> ReWrapHeldValuesAsync(int count, CancellationToken cancellationToken);
}
