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
/// Implements INT-MAIL-001, INT-MAIL-006, INT-MAIL-006a, INT-MAIL-007 and LIB-HOST-001.
/// A server that holds an account under the mailbox's name not carrying its identifier,
/// or one <c>integration.mailserver.ownaccounts</c> names whatever it carries, answers
/// <c>integration.mailserver.conflict</c> and changes nothing, save under
/// <see cref="MayAdopt"/> (D-254, D-255).
/// </remarks>
public sealed record MailboxPush(MailboxId Mailbox, Guid Key, EmailAddress Address, MailboxState State)
{
    /// <summary>
    /// Gets whether the push may take over an individual account the server holds under
    /// the mailbox's name that carries no mailbox identifier and that the deployment does
    /// not keep for itself, writing the mailbox's identifier into its description: the
    /// mailbox was reserved under <c>formerMailbox</c> <c>adopt</c> (D-255).
    /// </summary>
    public bool MayAdopt { get; init; }
}
