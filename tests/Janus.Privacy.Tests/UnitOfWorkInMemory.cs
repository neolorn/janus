using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests;

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
    /// How many were rolled back.
    /// </summary>
    public int RolledBack { get; private set; }

    /// <summary>
    /// Whether a transaction is open: begun and neither committed nor rolled back. A
    /// commit that fails leaves it rolled back.
    /// </summary>
    public bool Open => Opened > Committed + RolledBack + _failed;

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

    private int _failed;

    private List<Func<CancellationToken, ValueTask>> _afterCommit = [];

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

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public async ValueTask<Result> CommitAsync(CancellationToken cancellationToken)
    {
        if (RefusesCommit is Error refused)
        {
            RefusesCommit = null;
            _failed++;
            _afterCommit = [];

            return Result.Failure(refused);
        }

        Committed++;

        if (Open)
        {
            return Result.Success();
        }

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
        if (!Open)
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

        if (!Open)
        {
            _afterCommit = [];
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Forgets what was counted, so a test counts only the operation under test.
    /// </summary>
    public void Reset()
    {
        Opened = 0;
        Committed = 0;
        RolledBack = 0;
        _failed = 0;
        _afterCommit = [];
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
