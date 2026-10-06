using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What becomes of a mailbox someone has held when an invitation names its address
/// again: chapter 10 section 5.44.
/// </summary>
/// <remarks>
/// Implements REG-MAIL-003 and INT-MAIL-006. No mailbox anyone has held passes to a
/// holder without this choice, and the library removes such a mailbox only under
/// <see cref="Replace"/>.
/// </remarks>
public enum FormerMailbox
{
    /// <summary>
    /// The invitee receives the mailbox and the mail in it.
    /// </summary>
    [JsonStringEnumMemberName("transfer")]
    Transfer = 0,

    /// <summary>
    /// The old mailbox is removed and a new one reserved.
    /// </summary>
    [JsonStringEnumMemberName("replace")]
    Replace = 1,
}
