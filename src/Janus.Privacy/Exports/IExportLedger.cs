using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Exports;

/// <summary>
/// Where the exports an account has taken are counted. The rate limit needs to know
/// how many were assembled in a window and when the oldest of them was, which the
/// audit trail is not there to answer.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-003, D-086 and CONV-DESIGN-003. The row says that an export
/// was assembled and for whom, and holds nothing of what was in it.
/// </remarks>
internal interface IExportLedger
{
    /// <summary>
    /// Holds the subject's exports against every other export of theirs until the
    /// operation's transaction ends, so the window is counted on what is committed and
    /// two exports at once are counted one after the other (CONV-DESIGN-003).
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The work of holding them.</returns>
    /// <exception cref="System.InvalidOperationException">No transaction is open.</exception>
    ValueTask HoldAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// When each of the subject's exports since an instant was assembled.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="since">The instant to count from.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The instants, oldest first.</returns>
    ValueTask<IReadOnlyList<DateTimeOffset>> SinceAsync(
        SubjectId subject,
        DateTimeOffset since,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts one export.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="at">When it was assembled.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of counting it.</returns>
    ValueTask RecordAsync(SubjectId subject, DateTimeOffset at, CancellationToken cancellationToken);
}
