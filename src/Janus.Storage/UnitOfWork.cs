using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Janus.Storage;

/// <summary>
/// The one transaction an operation runs in, over the library's context.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements CONV-DESIGN-003 and OPS-DATA-002. The transaction is the context's own,
/// so a hand-written query taken through the connection accessor sees the writes made
/// before it in the same operation.
/// </remarks>
internal sealed class UnitOfWork(StoreContext context) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    private int _depth;

    /// <inheritdoc/>
    public async ValueTask BeginAsync(CancellationToken cancellationToken)
    {
        // An operation that calls another does not start a second transaction: the
        // outermost one is the one transaction the whole operation runs in, and the
        // inner call commits nothing of its own (CONV-DESIGN-003).
        if (_transaction is not null)
        {
            _depth++;

            return;
        }

        _transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("The operation has no transaction to commit.");
        }

        if (_depth > 0)
        {
            _depth--;

            return;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        await _transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_transaction is null)
        {
            return;
        }

        // Nothing committed, so nothing stays: an operation that failed part way
        // through leaves neither of its writes.
        await _transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        await _transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
        _depth = 0;
    }
}
