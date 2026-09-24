using System.Text.Json.Serialization;

namespace Janus.Authentication.Credentials;

/// <summary>
/// What a social provider's security event did, or why it did nothing, as its audit
/// record names it.
/// </summary>
/// <remarks>Implements IDN-LIFE-012a and IDN-AUD-001.</remarks>
internal enum ProviderEventOutcome
{
    /// <summary>
    /// Every session of the account ended and the linked credential is held until the
    /// person signs in by another factor.
    /// </summary>
    [JsonStringEnumMemberName("sessionsEnded")]
    SessionsEnded = 0,

    /// <summary>
    /// The linked credential was removed from the account.
    /// </summary>
    [JsonStringEnumMemberName("credentialUnlinked")]
    CredentialUnlinked = 1,

    /// <summary>
    /// The linked credential was the account's last, so the account is suspended and
    /// the security-notice set told.
    /// </summary>
    [JsonStringEnumMemberName("accountSuspended")]
    AccountSuspended = 2,

    /// <summary>
    /// The address the provider stopped vouching for dropped to unverified.
    /// </summary>
    [JsonStringEnumMemberName("addressUnverified")]
    AddressUnverified = 3,

    /// <summary>
    /// The event was recorded and changed nothing.
    /// </summary>
    [JsonStringEnumMemberName("recorded")]
    Recorded = 4,

    /// <summary>
    /// The event was refused: the provider's published keys do not verify it.
    /// </summary>
    [JsonStringEnumMemberName("unsigned")]
    Unsigned = 5,

    /// <summary>
    /// The event was refused: it had been carried before.
    /// </summary>
    [JsonStringEnumMemberName("replayed")]
    Replayed = 6,
}
