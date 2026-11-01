using System.Collections.Generic;
using Janus.Authentication.Mailboxes;

namespace Janus.Authentication.Invitations;

/// <summary>
/// A mailbox reservation judged and not yet written.
/// </summary>
/// <param name="Mailbox">The mailbox that will stand for the address.</param>
/// <param name="IsNew">Whether it is a new row, or one reserved again.</param>
/// <param name="Replaced">The expired invitations over the address that are withdrawn.</param>
/// <param name="Former">The mailbox replaced under <c>replace</c>, or nothing.</param>
/// <remarks>Implements REG-MAIL-001 and REG-MAIL-003.</remarks>
internal sealed record MailboxReservation(Mailbox Mailbox, bool IsNew, IReadOnlyList<Invitation> Replaced, Mailbox? Former);
