namespace Janus.Authentication.Callbacks;

/// <summary>
/// What a delivery of a host callback's event found when it claimed the event.
/// </summary>
/// <remarks>Implements BFF-MACH-002 AC3.</remarks>
internal enum CallbackClaim
{
    /// <summary>
    /// The delivery holds the claim, new or taken over from a delivery that never
    /// settled it, and is carried.
    /// </summary>
    Taken = 0,

    /// <summary>
    /// A delivery of the event was carried, so this one is acknowledged and not carried.
    /// </summary>
    Settled = 1,

    /// <summary>
    /// A delivery of the event is still being carried, its claim younger than
    /// <c>integration.callback.claimtimeout</c>.
    /// </summary>
    InProgress = 2,
}
