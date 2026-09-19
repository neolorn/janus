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
/// both tools see the same uncommitted writes.
/// </remarks>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Opens the transaction the operation runs in.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of opening it.</returns>
    ValueTask BeginAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Commits everything the operation wrote.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of committing it.</returns>
    ValueTask CommitAsync(CancellationToken cancellationToken);
}
