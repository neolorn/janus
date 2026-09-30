using System;

namespace Janus.Core;

/// <summary>
/// One change pushed to the mail server: the state one mailbox is to be in.
/// </summary>
/// <param name="Mailbox">
/// The mailbox's identifier, which a create writes on the account and which an account
/// the server already holds under the mailbox's name must carry before the push acts
/// on it.
/// </param>
/// <param name="Key">
/// What the push is recognised by. It stays the same for every attempt at the same
/// change, so a push made again produces nothing twice.
/// </param>
/// <param name="Address">The mailbox's address, in its canonical form.</param>
/// <param name="State">The state it is to be in.</param>
/// <remarks>
/// Implements INT-MAIL-001, INT-MAIL-006, INT-MAIL-006a and INT-MAIL-007. A server that
/// holds an account under the mailbox's name not carrying its identifier answers
/// <c>integration.mailserver.conflict</c> and changes nothing.
/// </remarks>
public sealed record MailboxPush(MailboxId Mailbox, Guid Key, string Address, MailboxState State);
