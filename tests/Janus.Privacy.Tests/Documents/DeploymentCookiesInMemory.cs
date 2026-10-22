using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Documents;

namespace Janus.Privacy.Tests.Documents;

/// <summary>
/// The cookies a deployment sets, as the test says they stand.
/// </summary>
internal sealed class DeploymentCookiesInMemory : IDeploymentCookies
{
    /// <summary>
    /// The cookies set now, by name.
    /// </summary>
    public IReadOnlyList<NoticeCookie> Set { get; set; } = [];

    /// <summary>
    /// What a read answers instead of the cookies, where a lifetime cannot be read.
    /// </summary>
    public Error? Failure { get; set; }

    /// <summary>
    /// How many times the cookies were read.
    /// </summary>
    public int Reads { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<NoticeCookie>>> ReadAsync(CancellationToken cancellationToken)
    {
        Reads++;

        return ValueTask.FromResult(Failure is Error failure
            ? Result.Failure<IReadOnlyList<NoticeCookie>>(failure)
            : Result.Success(Set));
    }
}
