using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The one transaction an operation runs in. A service method opens it, writes
/// everything the operation writes, and commits once at the end.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003 and OPS-DATA-002. An operation that began ends with a
/// commit or a rollback before it returns; disposal ends what a fault left open.
/// The same transaction is attached to the connection hand-written SQL runs on, so
/// both tools see the same uncommitted writes. Opening and committing name no failure
/// code: a caller that returns a result passes a failure up, and one that returns none
/// throws it as a fault naming its code (D-171).
/// </remarks>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Opens the transaction the operation runs in.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it opened.</returns>
    ValueTask<Result> BeginAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Commits everything the operation wrote.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it committed.</returns>
    ValueTask<Result> CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Ends the operation with nothing of it saved: the transaction, every change the
    /// scope tracks and every registration to run after the commit are discarded. An
    /// operation that joined a unit of work another opened ends its own level and marks
    /// the whole, so that nothing of it commits.
    /// </summary>
    /// <returns>A task that ends once the operation's level has ended.</returns>
    /// <remarks>
    /// It has no expected failure, so it answers no result and takes no cancellation
    /// token; a database error in it is a fault (CONV-DESIGN-003, CONV-CODE-002).
    /// </remarks>
    ValueTask RollbackAsync();
}
