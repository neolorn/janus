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

        Opened++;

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

        return ValueTask.FromResult(Result.Success());
    }

    /// <summary>
    /// Forgets what was counted, so a test counts only the operation under test.
    /// </summary>
    public void Reset()
    {
        Opened = 0;
        Committed = 0;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
