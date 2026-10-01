namespace Janus.Core;

/// <summary>
/// One account as the mail server's listing describes it.
/// </summary>
/// <param name="Mailbox">
/// The identifier of the library's mailbox the account carries, or nothing where it
/// carries none, as an account the library did not create does not.
/// </param>
/// <param name="Address">
/// Its address as the server lists it, in its canonical form, or nothing where what the
/// server lists does not read as an address, which is then no mailbox's address.
/// </param>
/// <param name="Enabled">Whether it accepts sign-in.</param>
/// <remarks>
/// Implements INT-MAIL-001, INT-MAIL-006a AC3 and INT-MAIL-007: reconciliation compares
/// each mailbox with the account listed under its identifier, enabled state as well as
/// existence, so two mailboxes a replacement leaves at one address stay apart.
/// </remarks>
public sealed record HostedMailbox(MailboxId? Mailbox, EmailAddress? Address, bool Enabled);
