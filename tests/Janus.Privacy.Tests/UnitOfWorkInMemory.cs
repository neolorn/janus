using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// The transaction, counting what was opened and what was committed so a test can
/// prove an operation writes once and commits once.
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

    /// <inheritdoc/>
    public ValueTask BeginAsync(CancellationToken cancellationToken)
    {
        Opened++;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CommitAsync(CancellationToken cancellationToken)
    {
        Committed++;

        return ValueTask.CompletedTask;
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
