using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// The deployment's answer to how far the caller authenticated, held in memory as a
/// deployment consuming authorization without this library's authentication reports
/// it.
/// </summary>
/// <param name="reached">What every caller has reached.</param>
internal sealed class AssuranceProviderInMemory(AssuranceLevel reached) : IAssuranceProvider
{
    /// <inheritdoc/>
    public ValueTask<Result<AssuranceLevel>> LevelAsync(
        AccessContext context,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(reached));
}
