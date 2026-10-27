using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What happened to a role.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004 and chapter 10 sections 5.39 and 5b (D-218), whose
/// <c>RoleChanged</c> row names the three.
/// </remarks>
public enum RoleChange
{
    /// <summary>
    /// The role was defined where none of its name stood, a declared role's seeding
    /// included.
    /// </summary>
    [JsonStringEnumMemberName("defined")]
    Defined = 0,

    /// <summary>
    /// The role's permissions or the roles it hands out changed.
    /// </summary>
    [JsonStringEnumMemberName("changed")]
    Changed = 1,

    /// <summary>
    /// The role, which nothing named, was removed.
    /// </summary>
    [JsonStringEnumMemberName("removed")]
    Removed = 2,
}
