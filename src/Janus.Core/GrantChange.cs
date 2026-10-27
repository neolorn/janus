using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What happened to a grant.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-001 and chapter 10 sections 5.39 and 5b (D-218), whose
/// <c>GrantChanged</c> row names the two.
/// </remarks>
public enum GrantChange
{
    /// <summary>
    /// The grant was written, a materialised derivation's grant included.
    /// </summary>
    [JsonStringEnumMemberName("made")]
    Made = 0,

    /// <summary>
    /// The grant was revoked, a materialised derivation's grant taken back included.
    /// </summary>
    [JsonStringEnumMemberName("revoked")]
    Revoked = 1,
}
