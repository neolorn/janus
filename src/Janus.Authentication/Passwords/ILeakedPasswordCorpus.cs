using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Passwords;

/// <summary>
/// Where the compromised-password corpus is read from. One prefix goes out and a
/// range comes back; the remainder of the hash is matched here, so neither the
/// password nor its full hash ever leaves the deployment.
/// </summary>
/// <remarks>Implements INT-PWD-001, INT-PWD-003, AUTH-PASS-004.</remarks>
internal interface ILeakedPasswordCorpus
{
    /// <summary>
    /// The hashes the corpus holds under one prefix.
    /// </summary>
    /// <param name="source">Which corpus to ask.</param>
    /// <param name="prefix">
    /// The first five upper-case hexadecimal characters of the hash.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The remainder of every hash the corpus holds under that prefix, or the failure
    /// where the corpus could not answer. An empty range is an answer; a failure is
    /// not.
    /// </returns>
    ValueTask<Result<IReadOnlySet<string>>> RangeAsync(
        BlocklistSource source,
        [NeverLogged] string prefix,
        CancellationToken cancellationToken);
}
