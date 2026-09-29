using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The one transaction an operation runs in. A service method opens it, writes
/// everything the operation writes, and commits once at the end.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003 and OPS-DATA-002. Disposal without a commit rolls the
/// transaction back, so an operation that returns a failure leaves nothing behind.
/// The same transaction is attached to the connection hand-written SQL runs on, so
/// both tools see the same uncommitted writes. Neither member names a failure code: a
/// caller that returns a result passes a failure up, and one that returns none throws it
/// as a fault naming its code (D-171).
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
}
