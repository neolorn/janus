namespace Janus.Core;

/// <summary>
/// A mailbox given to a current member of the administrative organization.
/// </summary>
/// <param name="Id">The mailbox's identifier.</param>
/// <param name="Address">Its address, in its canonical form.</param>
/// <param name="State">The state it is owed, chapter 10 section 5.45.</param>
/// <remarks>Implements REG-MAIL-001 and LIB-API-005 (D-253).</remarks>
public sealed record GivenMailbox(MailboxId Id, EmailAddress Address, MailboxState State);
