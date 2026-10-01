using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// The deployment's answer to how far the caller authenticated, held in memory as a
/// deployment consuming authorization without this library's authentication reports
/// it.
/// </summary>
/// <param name="attained">What every caller has attained, or nothing where the report cannot be read.</param>
internal sealed class AssuranceProviderInMemory(AttainedAssurance? attained) : IAssuranceProvider
{
    /// <inheritdoc/>
    public ValueTask<Result<AttainedAssurance>> AttainedAsync(
        AccessContext context,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(attained is null
            ? Result.Failure<AttainedAssurance>(Error.From(ErrorCodes.SystemFault))
            : Result.Success(attained));
}
