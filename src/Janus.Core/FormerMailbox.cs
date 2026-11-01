using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What becomes of a mailbox someone has held when an invitation names its address
/// again: chapter 10 section 5.44.
/// </summary>
/// <remarks>
/// Implements REG-MAIL-003 and INT-MAIL-006. No mailbox anyone has held passes to a
/// holder without this choice, and the library removes such a mailbox only under
/// <see cref="Replace"/>. <see cref="Adopt"/> names no mailbox the library holds: it lets
/// the push take over an account the mail server already holds (D-255).
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

    /// <summary>
    /// The push takes as the mailbox an individual account the mail server already holds
    /// under the address's local part and that carries no mailbox identifier, with its
    /// mail; where the server holds none, the push creates the mailbox (D-255).
    /// </summary>
    [JsonStringEnumMemberName("adopt")]
    Adopt = 2,
}
