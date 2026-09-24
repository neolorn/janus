namespace Janus.Hosting.Callbacks;

/// <summary>
/// The checks a host's callback passes on the machine profile, in the order it meets
/// them, each named in the entry recorded when it refuses.
/// </summary>
/// <remarks>Implements BFF-MACH-002, BFF-MACH-003 and INT-GEN-003.</remarks>
internal enum CallbackCheck
{
    /// <summary>The source is within <c>integration.callback.ratelimit</c>.</summary>
    RateLimit = 0,

    /// <summary>The source is within the ranges the provider publishes.</summary>
    Source = 1,

    /// <summary>The request carries a signature.</summary>
    Signature = 2,

    /// <summary>The signature was made within five minutes of the request's arrival.</summary>
    Window = 3,

    /// <summary>A presented signature is the one a live secret gives the signed bytes.</summary>
    Verification = 4,

    /// <summary>The verified request carries the provider's identifier of its event.</summary>
    Event = 5,

    /// <summary>The request carries a correlation reference issued for the callback.</summary>
    Reference = 6,

    /// <summary>The provider's API confirms what the request says.</summary>
    Confirmation = 7,
}
