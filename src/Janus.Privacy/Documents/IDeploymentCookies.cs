using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Documents;

/// <summary>
/// The cookies the deployment sets now: every one the library sets, with the lifetime
/// its configuration gives, and every one the host declares.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-006a (D-203). The library's cookies are the browser pipeline's,
/// so the list is read where the pipeline is.
/// </remarks>
internal interface IDeploymentCookies
{
    /// <summary>
    /// The cookies the deployment sets now, by name.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The cookies, or the failure where a lifetime could not be read.</returns>
    ValueTask<Result<IReadOnlyList<NoticeCookie>>> ReadAsync(CancellationToken cancellationToken);
}
