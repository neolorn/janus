using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// What giving a current member a mailbox carries, as the request reads.
/// </summary>
/// <param name="Address">The mailbox's address.</param>
/// <param name="FormerMailbox">
/// What becomes of a mailbox the address had, or <c>adopt</c> for an account the mail
/// server holds there; absent where neither stands.
/// </param>
/// <param name="Reason">Why, which the giving is recorded with.</param>
/// <remarks>Implements chapter 09 section 8a and REG-MAIL-001 (D-253, D-255).</remarks>
internal sealed record MailboxGiftBody(string? Address, FormerMailbox? FormerMailbox, string? Reason);
