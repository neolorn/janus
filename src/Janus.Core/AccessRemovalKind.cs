using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Which removal of access the host's check is asked about.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-281); the names are those the record of a refusal
/// carries in <c>details.kind</c>.
/// </remarks>
public enum AccessRemovalKind
{
    /// <summary>
    /// An allow grant is revoked.
    /// </summary>
    [JsonStringEnumMemberName("grantRevoked")]
    GrantRevoked = 0,

    /// <summary>
    /// A membership ends, now, when an end is set in advance, or when one is carried out.
    /// </summary>
    [JsonStringEnumMemberName("membershipEnded")]
    MembershipEnded = 1,

    /// <summary>
    /// A member is taken out of a group that reaches an allow grant.
    /// </summary>
    [JsonStringEnumMemberName("groupMemberRemoved")]
    GroupMemberRemoved = 2,
}
