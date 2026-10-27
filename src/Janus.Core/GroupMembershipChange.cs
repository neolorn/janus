using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What happened to a group's member.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001 and chapter 10 sections 5.39 and 5b (D-218), whose
/// <c>GroupMembershipChanged</c> row names the two.
/// </remarks>
public enum GroupMembershipChange
{
    /// <summary>
    /// The account or the group was added to the group.
    /// </summary>
    [JsonStringEnumMemberName("added")]
    Added = 0,

    /// <summary>
    /// The account or the group was taken out of the group, by a person or by a
    /// membership's end.
    /// </summary>
    [JsonStringEnumMemberName("removed")]
    Removed = 1,
}
