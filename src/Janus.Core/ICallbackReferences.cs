using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Issues the correlation references a host's unsigned callbacks are recognised by: the
/// host hands one to its provider with the operation, and the machine profile admits a
/// callback to that host's route only when it carries one issued for it.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-003, INT-GEN-003 and LIB-API-005. A reference is 128 random
/// bits in base64url, kept by its hash, so a copy of the database yields none a
/// callback could be forged with.
/// </remarks>
public interface ICallbackReferences
{
    /// <summary>
    /// Issues one reference for one of the host's unsigned callbacks.
    /// </summary>
    /// <param name="callback">The name the callback is mounted under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The reference, which is shown to the provider and to no person.</returns>
    ValueTask<Result<string>> IssueAsync(string callback, CancellationToken cancellationToken);
}
