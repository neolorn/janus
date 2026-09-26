using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// An environment whose restore fails and whose teardown then fails or throws, so the
/// instance it began may still stand.
/// </summary>
/// <param name="throws">Whether the teardown throws rather than reporting its failure.</param>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class RefusedRestore(bool throws) : IRestoreTestInstance
{
    /// <summary>
    /// How many times a teardown was asked for.
    /// </summary>
    public int TornDown { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Result<string>> RestoreAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure<string>(Error.From(ErrorCodes.SystemFault)));

    /// <inheritdoc/>
    public ValueTask<Result> TearDownAsync(CancellationToken cancellationToken)
    {
        TornDown++;

        return throws
            ? throw new InvalidOperationException("The instance could not be reached.")
            : ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
    }
}
