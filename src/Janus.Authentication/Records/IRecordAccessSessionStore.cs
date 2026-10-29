using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// Where record access sessions are held.
/// </summary>
/// <remarks>
/// Implements REC-ACC-001 and CONV-DESIGN-003. A session that has lapsed, been ended or
/// been replaced is removed and leaves nothing behind, which is why removal is a deletion
/// and not a state.
/// </remarks>
internal interface IRecordAccessSessionStore
{
    /// <summary>
    /// The session an identifier names.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session, or nothing where none answers to it.</returns>
    ValueTask<RecordAccessSession?> FindAsync(RecordAccessSessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// The session an identifier names, read under a lock on its row held until the
    /// operation's transaction ends (CONV-DESIGN-003).
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session as committed when the lock was taken, or nothing.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask<RecordAccessSession?> FindForUpdateAsync(RecordAccessSessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new session.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of storing it.</returns>
    ValueTask AddAsync(RecordAccessSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Stores what a step changed.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of storing it.</returns>
    ValueTask RecordAsync(RecordAccessSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a session, ended or replaced.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(RecordAccessSessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every session reaching a record, in the operation's transaction
    /// (REC-TKD-001).
    /// </summary>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many it ended.</returns>
    ValueTask<int> EndReachingAsync(SubjectId record, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every session that has lapsed.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were removed.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
