using System;
using System.Collections.Generic;
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

    // An operation that joined the unit of work rolled back, so nothing of it commits.
    private bool _marked;

    // What runs once the outermost transaction has committed; a rollback discards it.
    private List<Func<CancellationToken, ValueTask>> _afterCommit = [];

    /// <inheritdoc/>
    public async ValueTask<Result> BeginAsync(CancellationToken cancellationToken)
    {
        // An operation that calls another does not start a second transaction: the
        // outermost one is the one transaction the whole operation runs in, and the
        // inner call commits nothing of its own (CONV-DESIGN-003).
        if (_transaction is not null)
        {
            _depth++;

            return Result.Success();
        }

        _transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("The operation has no transaction to commit.");
        }

        if (_depth > 0)
        {
            _depth--;

            return Result.Success();
        }

        if (_marked)
        {
            await EndAsync(CancellationToken.None).ConfigureAwait(false);

            throw new InvalidOperationException(
                "An operation inside the unit of work rolled back, so nothing of it commits.");
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // A commit that fails leaves the unit of work rolled back, so the scope's
            // next operation saves nothing of this one.
            await EndAsync(CancellationToken.None).ConfigureAwait(false);

            throw;
        }

        await _transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;

        // CONV-DESIGN-002: what was registered runs now, outside any transaction. Each
        // may begin and commit a unit of work of its own in this scope, so the list is
        // taken before the first runs and nothing registered meanwhile is run twice.
        List<Func<CancellationToken, ValueTask>> registered = _afterCommit;

        _afterCommit = [];

        foreach (Func<CancellationToken, ValueTask> work in registered)
        {
            await work(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public Result AfterCommit(Func<CancellationToken, ValueTask> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (_transaction is null)
        {
            throw new InvalidOperationException("No unit of work is in progress to register work on.");
        }

        _afterCommit.Add(work);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask RollbackAsync()
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("The operation has no transaction to roll back.");
        }

        // An inner level ends itself and marks the whole: the outermost level is the one
        // that holds the transaction, and it is the one that rolls it back.
        if (_depth > 0)
        {
            _depth--;
            _marked = true;

            return;
        }

        await EndAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_transaction is null)
        {
            return;
        }

        // Nothing committed, so nothing stays: an operation a fault ended part way
        // through leaves neither of its writes.
        await EndAsync(CancellationToken.None).ConfigureAwait(false);
    }

    // Leaves the unit of work as though no operation had begun it: the transaction
    // rolled back, and nothing tracked for a later commit to save.
    private async ValueTask EndAsync(CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = _transaction!;

        _transaction = null;
        _depth = 0;
        _marked = false;
        _afterCommit = [];
        context.ChangeTracker.Clear();

        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        await transaction.DisposeAsync().ConfigureAwait(false);
    }
}
