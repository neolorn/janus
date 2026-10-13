namespace Janus.Hosting.Credentials;

/// <summary>
/// What verifying a social provider's security event against what the provider
/// publishes found: that it verifies, the first of the failures in the order chapter 09
/// section 10 fixes, or that the provider's document could not be read, which is no
/// failure of the event.
/// </summary>
/// <remarks>Implements IDN-LIFE-012a AC8 and chapter 09 section 10.</remarks>
internal enum ProviderEventVerification
{
    /// <summary>The event is the provider's, addressed to the deployment and inside any lifetime it states.</summary>
    Verified = 0,

    /// <summary>The deployment declared no such provider.</summary>
    Undeclared = 1,

    /// <summary>The provider's metadata or its key set could not be had or read.</summary>
    Unreadable = 2,

    /// <summary>The key the event names is not in the published set, or its signature does not verify.</summary>
    Key = 3,

    /// <summary>The event's issuer is not the one the provider's document names.</summary>
    Issuer = 4,

    /// <summary>The event names none of the declared clients as its audience.</summary>
    Audience = 5,

    /// <summary>The lifetime the event states has passed.</summary>
    Lifetime = 6,
}
