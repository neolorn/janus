namespace Janus.Core;

/// <summary>
/// One mailbox as the mail server's listing describes it.
/// </summary>
/// <param name="Address">Its address, in its canonical form.</param>
/// <param name="Enabled">Whether it accepts sign-in.</param>
/// <remarks>
/// Implements INT-MAIL-006a AC3 and INT-MAIL-007: reconciliation compares enabled state
/// as well as existence.
/// </remarks>
public sealed record HostedMailbox(string Address, bool Enabled);
