using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Callbacks;

/// <summary>
/// The correlation references issued for a host's unsigned callbacks, each kept as its
/// hash and never itself.
/// </summary>
/// <remarks>Implements BFF-MACH-003, INT-GEN-003 and CONV-DESIGN-003.</remarks>
internal interface ICallbackReferenceStore
{
    /// <summary>
    /// Keeps one issued reference.
    /// </summary>
    /// <param name="callback">The callback it was issued for.</param>
    /// <param name="reference">Its hash.</param>
    /// <param name="at">When it was issued.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of keeping it.</returns>
    ValueTask AddAsync(
        string callback,
        byte[] reference,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a reference was issued for a callback.
    /// </summary>
    /// <param name="callback">The callback it arrived on.</param>
    /// <param name="reference">The hash of what it carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it was.</returns>
    ValueTask<bool> HoldsAsync(string callback, byte[] reference, CancellationToken cancellationToken);
}
