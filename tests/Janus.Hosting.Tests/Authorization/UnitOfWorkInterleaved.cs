using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The unit of work of the deployment, with one thing another connection commits in
/// the moment before it begins: what happens between an operation's gate step and its
/// transaction (AUTHZ-GATE-006).
/// </summary>
/// <param name="inner">The unit of work the operation runs in.</param>
/// <param name="meanwhile">What is committed elsewhere before it begins.</param>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class UnitOfWorkInterleaved(IUnitOfWork inner, Func<CancellationToken, Task> meanwhile)
    : IUnitOfWork
{
    private bool _interleaved;

    /// <summary>
    /// Gets a value indicating whether a transaction the operation began is still open.
    /// </summary>
    public bool Open { get; private set; }

    /// <inheritdoc/>
    public async ValueTask<Result> BeginAsync(CancellationToken cancellationToken)
    {
        if (!_interleaved)
        {
            _interleaved = true;

            await meanwhile(cancellationToken).ConfigureAwait(false);
        }

        Result begun = await inner.BeginAsync(cancellationToken).ConfigureAwait(false);

        Open = begun.Match(() => true, _ => Open);

        return begun;
    }

    /// <inheritdoc/>
    public async ValueTask<Result> CommitAsync(CancellationToken cancellationToken)
    {
        Result committed = await inner.CommitAsync(cancellationToken).ConfigureAwait(false);

        Open = false;

        return committed;
    }

    /// <inheritdoc/>
    public Result AfterCommit(Func<CancellationToken, ValueTask> work) => inner.AfterCommit(work);

    /// <inheritdoc/>
    public async ValueTask RollbackAsync()
    {
        await inner.RollbackAsync().ConfigureAwait(false);

        Open = false;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
