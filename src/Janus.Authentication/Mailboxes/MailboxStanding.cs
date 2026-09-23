namespace Janus.Authentication.Mailboxes;

/// <summary>
/// A mailbox with whether its holder stands, which is what decides the state it is
/// owed.
/// </summary>
/// <param name="Mailbox">The mailbox.</param>
/// <param name="Stands">
/// Whether its holder is an active account with a current membership of the
/// administrative organization. False for a mailbox nobody holds.
/// </param>
/// <remarks>Implements INT-MAIL-006a.</remarks>
internal sealed record MailboxStanding(Mailbox Mailbox, bool Stands);
