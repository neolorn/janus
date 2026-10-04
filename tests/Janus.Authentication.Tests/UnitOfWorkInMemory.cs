using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests;

/// <summary>
/// The transaction, counting what was opened, committed and rolled back so a test can
/// prove an operation writes once and ends once, refusing to open or commit where a
/// test asks it to (CONV-DESIGN-003 AC5, AC7 and AC8), and running what was registered
/// on it once its outermost level commits, as the store's does (CONV-DESIGN-002).
/// </summary>
internal sealed class UnitOfWorkInMemory : IUnitOfWork
{
    private readonly List<Action> _undo = [];

    private List<Func<CancellationToken, ValueTask>> _afterCommit = [];

    private int _depth;

    private bool _marked;

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
    /// How many registrations a rollback, or a commit that failed, discarded unrun.
    /// </summary>
    public int Discarded { get; private set; }

    /// <summary>
    /// Whether a transaction is open: begun and neither committed nor rolled back.
    /// </summary>
    public bool Open => _depth > 0;

    /// <summary>
    /// What another transaction commits as the next one opens, where a test sets it: a
    /// change made after an operation's gate step and before its first write. It
    /// happens once.
    /// </summary>
    public Action? Meanwhile { get; set; }

    /// <summary>
    /// The failure the next opening answers, where a test sets one.
    /// </summary>
    public Error? RefusesBegin { get; set; }

    /// <summary>
    /// The failure the next commit answers, where a test sets one.
    /// </summary>
    public Error? RefusesCommit { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result> BeginAsync(CancellationToken cancellationToken)
    {
        if (RefusesBegin is Error refused)
        {
            RefusesBegin = null;

            return ValueTask.FromResult(Result.Failure(refused));
        }

        if (Meanwhile is Action meanwhile)
        {
            Meanwhile = null;
            meanwhile();
        }

        Opened++;
        _depth++;

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public async ValueTask<Result> CommitAsync(CancellationToken cancellationToken)
    {
        if (RefusesCommit is Error refused)
        {
            RefusesCommit = null;

            // A commit that fails leaves the unit of work rolled back.
            _depth = 0;
            _marked = false;
            Discard();

            return Result.Failure(refused);
        }

        _depth = Math.Max(_depth - 1, 0);

        if (_depth is 0 && _marked)
        {
            _marked = false;
            RolledBack++;
            Discard();

            throw new InvalidOperationException(
                "An operation inside the unit of work rolled back, so nothing of it commits.");
        }

        Committed++;

        if (_depth is not 0)
        {
            return Result.Success();
        }

        OutermostCommitted++;
        _undo.Clear();

        List<Func<CancellationToken, ValueTask>> registered = _afterCommit;

        _afterCommit = [];

        foreach (Func<CancellationToken, ValueTask> work in registered)
        {
            await work(cancellationToken);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public Result AfterCommit(Func<CancellationToken, ValueTask> work)
    {
        if (_depth is 0)
        {
            throw new InvalidOperationException("No unit of work is in progress to register work on.");
        }

        _afterCommit.Add(work);

        return Result.Success();
    }

    /// <inheritdoc/>
    public ValueTask RollbackAsync()
    {
        RolledBack++;
        _depth = Math.Max(_depth - 1, 0);
        _marked = _depth > 0;

        if (_depth is 0)
        {
            Discard();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Has a fake that keeps state put it back where the unit of work in progress does
    /// not commit, as the database forgets what a transaction that rolled back wrote.
    /// </summary>
    /// <param name="undo">What puts the state back as it stood.</param>
    public void Undoing(Action undo)
    {
        if (_depth > 0)
        {
            _undo.Add(undo);
        }
    }

    /// <summary>
    /// Forgets what was counted, so a test counts only the operation under test.
    /// </summary>
    public void Reset()
    {
        _undo.Clear();
        Opened = 0;
        Committed = 0;
        OutermostCommitted = 0;
        RolledBack = 0;
        Discarded = 0;
        _depth = 0;
        _marked = false;
        _afterCommit = [];
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Nothing of the unit of work stays: what was registered is discarded unrun, and
    // each fake that enlisted puts back what it held, the latest write first.
    private void Discard()
    {
        Discarded += _afterCommit.Count;
        _afterCommit = [];

        for (int index = _undo.Count - 1; index >= 0; index--)
        {
            _undo[index]();
        }

        _undo.Clear();
    }
}
