using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Records;

/// <summary>
/// Where collection sessions are held. Nothing else in the deployment knows one exists:
/// it reserves nothing and writes into no other table.
/// </summary>
/// <remarks>
/// Implements REC-COL-001 and CONV-DESIGN-003. A session that has lapsed, been abandoned
/// or been spent is removed and leaves nothing behind, which is why removal is a
/// deletion and not a state.
/// </remarks>
internal interface ICollectionSessionStore
{
    /// <summary>
    /// The session an identifier names.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session, or nothing where none answers to it.</returns>
    ValueTask<CollectionSession?> FindAsync(CollectionSessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// The session an identifier names, read under a lock on its row held until the
    /// operation's transaction ends, so answers given to it at once are decided one
    /// after another (CONV-DESIGN-003).
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The session as committed when the lock was taken, or nothing.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask<CollectionSession?> FindForUpdateAsync(CollectionSessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new session.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of storing it.</returns>
    ValueTask AddAsync(CollectionSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Stores what a step changed.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of storing it.</returns>
    ValueTask RecordAsync(CollectionSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a session, abandoned or spent.
    /// </summary>
    /// <param name="id">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(CollectionSessionId id, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every session that has lapsed.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were removed.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
