using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// What an administrator chose for a mailbox someone has held, and why, as the
/// invitation that takes it over records it.
/// </summary>
/// <param name="Choice">What becomes of the mailbox.</param>
/// <param name="Reason">Why, as given.</param>
/// <remarks>Implements REG-MAIL-003 and chapter 10 section 5.44.</remarks>
internal sealed record MailboxTakeover(FormerMailbox Choice, string Reason);
