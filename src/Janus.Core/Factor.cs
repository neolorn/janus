using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// An entry of the factor catalogue, by the identifier requests, credential records
/// and a policy's login factors use.
/// </summary>
/// <remarks>Implements AUTH-FACT-002, chapter 10 section 4.1a.</remarks>
public enum Factor
{
    /// <summary>
    /// A password.
    /// </summary>
    [JsonStringEnumMemberName("password")]
    Password = 0,

    /// <summary>
    /// A discoverable WebAuthn credential, over any transport including hybrid.
    /// </summary>
    [JsonStringEnumMemberName("passkey")]
    Passkey = 1,

    /// <summary>
    /// A sign-in link sent by email.
    /// </summary>
    [JsonStringEnumMemberName("emailLink")]
    EmailLink = 2,

    /// <summary>
    /// A sign-in code sent by email.
    /// </summary>
    [JsonStringEnumMemberName("emailCode")]
    EmailCode = 3,

    /// <summary>
    /// A sign-in link sent by SMS.
    /// </summary>
    [JsonStringEnumMemberName("phoneLink")]
    PhoneLink = 4,

    /// <summary>
    /// Google, which asserts a credential and no tier.
    /// </summary>
    [JsonStringEnumMemberName("google")]
    Google = 5,

    /// <summary>
    /// Apple, which asserts a credential and no tier.
    /// </summary>
    [JsonStringEnumMemberName("apple")]
    Apple = 6,

    /// <summary>
    /// A time-based one-time password.
    /// </summary>
    [JsonStringEnumMemberName("totp")]
    Totp = 7,

    /// <summary>
    /// A non-discoverable WebAuthn credential used as a second step.
    /// </summary>
    [JsonStringEnumMemberName("securityKey")]
    SecurityKey = 8,

    /// <summary>
    /// A code sent by SMS as a second step.
    /// </summary>
    [JsonStringEnumMemberName("phoneCode")]
    PhoneCode = 9,

    /// <summary>
    /// A single-use recovery code.
    /// </summary>
    [JsonStringEnumMemberName("recoveryCodes")]
    RecoveryCodes = 10,

    /// <summary>
    /// The emergency credential, which satisfies every gate for its session's
    /// lifetime and never appears in a policy's login factors.
    /// </summary>
    [JsonStringEnumMemberName("breakGlass")]
    BreakGlass = 11,
}
