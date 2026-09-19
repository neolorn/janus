using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// Where erasures are read and written.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003b and CONV-DESIGN-003. Nothing here begins an erasure: the
/// row is written by the erasure itself, in the transaction that destroys the key.
/// </remarks>
internal interface IErasureStore
{
    /// <summary>
    /// Reads one subject's erasure.
    /// </summary>
    /// <param name="subject">Whose erasure to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The erasure, or nothing where the subject was never erased.</returns>
    ValueTask<Erasure?> FindBySubjectAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Reads every erasure whose host-side work is outstanding, in one query: the ones
    /// awaiting subscribers and the ones a subscriber failed.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The erasures, oldest first.</returns>
    ValueTask<IReadOnlyList<Erasure>> FindIncompleteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Carries the progress the erasure has made onto its row.
    /// </summary>
    /// <param name="erasure">The erasure as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="System.InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(Erasure erasure, CancellationToken cancellationToken);
}
