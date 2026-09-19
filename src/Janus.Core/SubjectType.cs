using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a grant's subject is: the person named, or a group they are in.
/// </summary>
/// <remarks>Implements chapter 10 section 5.5, AUTHZ-GRANT-001.</remarks>
public enum SubjectType
{
    /// <summary>
    /// One account.
    /// </summary>
    [JsonStringEnumMemberName("user")]
    User = 0,

    /// <summary>
    /// A group, whose members hold the grant transitively.
    /// </summary>
    [JsonStringEnumMemberName("group")]
    Group = 1,
}
