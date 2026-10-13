using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Callbacks;

/// <summary>
/// The provider event identifiers a callback has been claimed for, so that a delivery
/// the provider repeats is carried once.
/// </summary>
/// <remarks>Implements BFF-MACH-002, INT-GEN-003 and CONV-DESIGN-003.</remarks>
internal interface ICallbackEvents
{
    /// <summary>
    /// Claims one event for one host callback: inserts an unsettled claim where the
    /// event holds none, and takes over one left unsettled since
    /// <paramref name="stale"/> or before, in one statement that holds the event's
    /// row until the transaction ends.
    /// </summary>
    /// <param name="callback">The callback's name.</param>
    /// <param name="identifier">The hash of the provider's event identifier.</param>
    /// <param name="at">When the delivery arrived, which the claim records.</param>
    /// <param name="stale">The latest claim a delivery may take over.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether this delivery holds the claim, or what holds it instead.</returns>
    ValueTask<CallbackClaim> ClaimAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset at,
        DateTimeOffset stale,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims one provider event settled, in the transaction its work runs in, where
    /// no delivery of it has been claimed yet.
    /// </summary>
    /// <param name="callback">The callback's name.</param>
    /// <param name="identifier">The hash of the provider's event identifier.</param>
    /// <param name="at">When the delivery arrived.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether this delivery holds the claim; false where one already did.</returns>
    ValueTask<bool> CarryAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Settles the claim a delivery holds once its route has carried it, so every later
    /// delivery of the event is acknowledged without being carried.
    /// </summary>
    /// <param name="callback">The callback's name.</param>
    /// <param name="identifier">The hash of the provider's event identifier.</param>
    /// <param name="claimed">When the delivery took the claim, which says the claim is its own.</param>
    /// <param name="at">When the route carried it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of settling it.</returns>
    ValueTask SettleAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset claimed,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gives back the unsettled claim a delivery holds, so the provider's next delivery
    /// of the event is carried.
    /// </summary>
    /// <param name="callback">The callback's name.</param>
    /// <param name="identifier">The hash of the provider's event identifier.</param>
    /// <param name="claimed">When the delivery took the claim, which says the claim is its own.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of giving it back.</returns>
    ValueTask ReleaseAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset claimed,
        CancellationToken cancellationToken);
}
