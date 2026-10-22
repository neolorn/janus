using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The unit of work of the deployment, whose commit fails: it rolls the transaction
/// back, as a commit that fails leaves it, and answers the failure (CONV-DESIGN-003).
/// </summary>
/// <param name="inner">The unit of work the operation runs in.</param>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class UnitOfWorkRefusingCommit(IUnitOfWork inner) : IUnitOfWork
{
    /// <inheritdoc/>
    public ValueTask<Result<bool>> BeginAsync(CancellationToken cancellationToken) =>
        inner.BeginAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<Result> CommitAsync(CancellationToken cancellationToken)
    {
        await inner.RollbackAsync().ConfigureAwait(false);

        return Result.Failure(Error.From(ErrorCodes.SystemFault));
    }

    /// <inheritdoc/>
    public Result AfterCommit(Func<CancellationToken, ValueTask> work) => inner.AfterCommit(work);

    /// <inheritdoc/>
    public ValueTask RollbackAsync() => inner.RollbackAsync();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
