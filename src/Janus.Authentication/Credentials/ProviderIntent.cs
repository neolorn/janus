using System.Text.Json.Serialization;

namespace Janus.Authentication.Credentials;

/// <summary>
/// What a round trip to a social provider is for, which decides what its return does
/// with the identity the provider vouched for.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012, REG-IDENT-008 and AUTH-FACT-002a. Each member is named as
/// the <c>intent</c> a round trip is started with.
/// </remarks>
internal enum ProviderIntent
{
    /// <summary>
    /// Signing in with an identity already linked to an account.
    /// </summary>
    [JsonStringEnumMemberName("signin")]
    SignIn = 0,

    /// <summary>
    /// Supplying the email step of the registration the browser has in flight, and the
    /// credential the account is created with.
    /// </summary>
    [JsonStringEnumMemberName("register")]
    Register = 1,

    /// <summary>
    /// Attaching the identity to the account the browser is signed in to.
    /// </summary>
    [JsonStringEnumMemberName("link")]
    Link = 2,
}
