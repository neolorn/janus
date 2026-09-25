using System.Collections.Generic;

namespace Janus.Authentication.Mailboxes;

/// <summary>
/// What one reconciliation found between the mailboxes the library provisions and the
/// ones the mail server hosts.
/// </summary>
/// <param name="Mailboxes">
/// The library's mailboxes the server holds in another state, holds when it should not,
/// or does not hold at all.
/// </param>
/// <param name="Unknown">How many hosted mailboxes the library has no row for.</param>
/// <remarks>Implements INT-MAIL-006a AC3 and INT-MAIL-007 AC2.</remarks>
internal sealed record MailboxDrift(IReadOnlyList<MailboxId> Mailboxes, int Unknown)
{
    /// <summary>
    /// Whether both sides agree.
    /// </summary>
    public bool IsEmpty => Mailboxes.Count is 0 && Unknown is 0;
}
