using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Callbacks;

/// <summary>
/// The provider event identifiers a callback has been carried for, so that a delivery
/// the provider repeats is carried once.
/// </summary>
/// <remarks>Implements BFF-MACH-002, INT-GEN-003 and CONV-DESIGN-003.</remarks>
internal interface ICallbackEvents
{
    /// <summary>
    /// Claims one event for one callback, where no delivery of it has been claimed yet.
    /// </summary>
    /// <param name="callback">The callback's name.</param>
    /// <param name="identifier">The hash of the provider's event identifier.</param>
    /// <param name="at">When the delivery arrived.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether this delivery holds the claim; false where one already did.</returns>
    ValueTask<bool> ClaimAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gives a claim back, so the provider's next delivery of the event is carried.
    /// </summary>
    /// <param name="callback">The callback's name.</param>
    /// <param name="identifier">The hash of the provider's event identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of giving it back.</returns>
    ValueTask ReleaseAsync(string callback, byte[] identifier, CancellationToken cancellationToken);
}
