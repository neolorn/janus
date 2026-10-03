using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests;

/// <summary>
/// The transaction, counting what was opened, committed and rolled back so a test can
/// prove an operation writes once and ends once, and refusing to open or commit where a
/// test asks it to (CONV-DESIGN-003 AC5, AC7 and AC8).
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
    /// How many levels were rolled back.
    /// </summary>
    public int RolledBack { get; private set; }

    /// <summary>
    /// Whether a transaction is open: begun and neither committed nor rolled back.
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

    private bool _marked;

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

            // A commit that fails leaves the unit of work rolled back.
            _depth = 0;
            _marked = false;

            return ValueTask.FromResult(Result.Failure(refused));
        }

        _depth = Math.Max(_depth - 1, 0);

        if (_depth is 0 && _marked)
        {
            _marked = false;
            RolledBack++;

            throw new InvalidOperationException(
                "An operation inside the unit of work rolled back, so nothing of it commits.");
        }

        Committed++;

        if (_depth is 0)
        {
            OutermostCommitted++;
        }

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public ValueTask RollbackAsync()
    {
        RolledBack++;
        _depth = Math.Max(_depth - 1, 0);
        _marked = _depth > 0;

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Forgets what was counted, so a test counts only the operation under test.
    /// </summary>
    public void Reset()
    {
        Opened = 0;
        Committed = 0;
        OutermostCommitted = 0;
        RolledBack = 0;
        _depth = 0;
        _marked = false;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
