using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What happened to a membership.
/// </summary>
/// <remarks>
/// Implements IDN-MEM-001 and chapter 10 section 5b, whose <c>MembershipChanged</c> row
/// names the two.
/// </remarks>
public enum MembershipChange
{
    /// <summary>
    /// The membership attached, and the organization's policy governs the account.
    /// </summary>
    [JsonStringEnumMemberName("began")]
    Began = 0,

    /// <summary>
    /// The membership ended. The account and the organization persist, and the
    /// record stays where it is.
    /// </summary>
    [JsonStringEnumMemberName("ended")]
    Ended = 1,
}
