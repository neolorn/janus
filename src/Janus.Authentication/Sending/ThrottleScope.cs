using System.Text.Json.Serialization;

namespace Janus.Authentication.Sending;

/// <summary>
/// What a progressive delay is counted against. The three are independent: one
/// attacker cannot raise another person's delay by any of them alone.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-001.</remarks>
internal enum ThrottleScope
{
    /// <summary>
    /// The address the attempts came from.
    /// </summary>
    [JsonStringEnumMemberName("source")]
    Source = 0,

    /// <summary>
    /// The account they were made against.
    /// </summary>
    [JsonStringEnumMemberName("account")]
    Account = 1,

    /// <summary>
    /// The identifier that was typed, which exists whether or not an account holds
    /// it and is what makes the answer the same either way (AUTH-ABUSE-002).
    /// </summary>
    [JsonStringEnumMemberName("identifier")]
    Identifier = 2,
}
