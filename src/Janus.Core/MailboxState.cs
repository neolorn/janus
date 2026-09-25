using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The state a mailbox is pushed to.
/// </summary>
/// <remarks>
/// Implements INT-MAIL-006 and INT-MAIL-006a. A disabled mailbox accepts no sign-in and
/// no application password, and still exists, so mail to it is accepted and nothing is
/// lost.
/// </remarks>
public enum MailboxState
{
    /// <summary>
    /// Exists and accepts no sign-in: reserved for an invitation, or held by an
    /// account that is not active or no longer a member.
    /// </summary>
    [JsonStringEnumMemberName("disabled")]
    Disabled = 0,

    /// <summary>
    /// Exists and serves its holder.
    /// </summary>
    [JsonStringEnumMemberName("enabled")]
    Enabled = 1,

    /// <summary>
    /// Given up before anyone held it: the invitation it was reserved for was revoked.
    /// </summary>
    [JsonStringEnumMemberName("removed")]
    Removed = 2,
}
