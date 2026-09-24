using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// A restore that never finishes, and gives up only when it is abandoned.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class StalledRestore : IRestoreTestInstance
{
    /// <summary>
    /// How many times a teardown was asked for.
    /// </summary>
    public int TornDown { get; private set; }

    /// <inheritdoc/>
    public async ValueTask<Result<string>> RestoreAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        return Result.Success(string.Empty);
    }

    /// <inheritdoc/>
    public ValueTask<Result> TearDownAsync(CancellationToken cancellationToken)
    {
        TornDown++;

        return ValueTask.FromResult(Result.Success());
    }
}
