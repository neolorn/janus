namespace Janus.Authentication.Credentials;

/// <summary>
/// What a social provider's security event says of the identity it concerns, which
/// decides what the library does about it.
/// </summary>
/// <remarks>Implements IDN-LIFE-012a.</remarks>
internal enum ProviderEventKind
{
    /// <summary>
    /// Nothing the library acts on: the event is recorded.
    /// </summary>
    Informational = 0,

    /// <summary>
    /// The provider account was compromised or disabled, or its sessions were revoked.
    /// </summary>
    Compromised = 1,

    /// <summary>
    /// The person withdrew consent at the provider, or deleted the provider account.
    /// </summary>
    Withdrawn = 2,

    /// <summary>
    /// The provider stopped forwarding mail to an address it operates.
    /// </summary>
    AddressDisabled = 3,
}
