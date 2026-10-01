using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests;

/// <summary>
/// The transaction, counting what was opened and what was committed so a test can
/// prove an operation writes once and commits once, and refusing to open or commit
/// where a test asks it to (CONV-DESIGN-003 AC7).
/// </summary>
internal sealed class UnitOfWorkInMemory : IUnitOfWork
{
    /// <summary>
    /// How many transactions were opened.
    /// </summary>
    public int Opened { get; private set; }

    /// <summary>
    /// How many were committed.
    /// </summary>
    public int Committed { get; private set; }

    /// <summary>
    /// How many outermost transactions were committed. A transaction opened inside
    /// another joins it and its commit is the outer one's to make, as the store's is
    /// (CONV-DESIGN-003).
    /// </summary>
    public int OutermostCommitted { get; private set; }

    /// <summary>
    /// Whether a transaction is open: begun and not yet committed.
    /// </summary>
    public bool Open => _depth > 0;

    /// <summary>
    /// The failure the next opening answers, where a test sets one.
    /// </summary>
    public Error? RefusesBegin { get; set; }

    /// <summary>
    /// The failure the next commit answers, where a test sets one.
    /// </summary>
    public Error? RefusesCommit { get; set; }

    private int _depth;

    /// <inheritdoc/>
    public ValueTask<Result> BeginAsync(CancellationToken cancellationToken)
    {
        if (RefusesBegin is Error refused)
        {
            RefusesBegin = null;

            return ValueTask.FromResult(Result.Failure(refused));
        }

        Opened++;
        _depth++;

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public ValueTask<Result> CommitAsync(CancellationToken cancellationToken)
    {
        if (RefusesCommit is Error refused)
        {
            RefusesCommit = null;

            return ValueTask.FromResult(Result.Failure(refused));
        }

        Committed++;
        _depth = Math.Max(_depth - 1, 0);

        if (_depth is 0)
        {
            OutermostCommitted++;
        }

        return ValueTask.FromResult(Result.Success());
    }

    /// <summary>
    /// Forgets what was counted, so a test counts only the operation under test.
    /// </summary>
    public void Reset()
    {
        Opened = 0;
        Committed = 0;
        OutermostCommitted = 0;
        _depth = 0;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
