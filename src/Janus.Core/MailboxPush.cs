using System;

namespace Janus.Core;

/// <summary>
/// One change pushed to the mail server: the state one mailbox is to be in.
/// </summary>
/// <param name="Key">
/// What the push is recognised by. It stays the same for every attempt at the same
/// change, so a push made again produces nothing twice.
/// </param>
/// <param name="Address">The mailbox's address, in its canonical form.</param>
/// <param name="State">The state it is to be in.</param>
/// <remarks>Implements INT-MAIL-006, INT-MAIL-006a and INT-MAIL-007.</remarks>
public sealed record MailboxPush(Guid Key, string Address, MailboxState State);
