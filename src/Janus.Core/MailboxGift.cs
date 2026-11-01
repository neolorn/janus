namespace Janus.Core;

/// <summary>
/// What giving a current member of the administrative organization a mailbox names.
/// </summary>
/// <param name="Address">The mailbox's address, as entered.</param>
/// <param name="Reason">Why, which the giving is recorded with.</param>
/// <remarks>Implements REG-MAIL-001 and REG-MAIL-003 (D-253, D-255).</remarks>
public sealed record MailboxGift(string Address, string Reason)
{
    /// <summary>
    /// What becomes of a mailbox the address had, or <c>adopt</c> for an account the mail
    /// server holds there; nothing where neither stands.
    /// </summary>
    public FormerMailbox? FormerMailbox { get; init; }
}
