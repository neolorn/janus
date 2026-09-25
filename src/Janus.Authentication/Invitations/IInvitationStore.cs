using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Mailboxes;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// Where invitations are read and written.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-009a, REG-INV-001, REG-MAIL-001 and CONV-DESIGN-003. An
/// invitation is added and changed inside the transaction that made the change true,
/// so nothing here commits.
/// </remarks>
internal interface IInvitationStore
{
    /// <summary>
    /// Reads one invitation.
    /// </summary>
    /// <param name="id">Which invitation.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The invitation, or nothing where no such row exists.</returns>
    ValueTask<Invitation?> FindAsync(InvitationId id, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the invitation a link's token opens.
    /// </summary>
    /// <param name="token">What is stored against the token.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The invitation, or nothing where no invitation was issued with it.</returns>
    ValueTask<Invitation?> FindByTokenAsync(byte[] token, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the invitation an account opened the link of most recently, among those
    /// that still stand.
    /// </summary>
    /// <param name="invitee">The account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The invitation, neither revoked nor acknowledged, expired or not; nothing where
    /// none is attached to the account.
    /// </returns>
    ValueTask<Invitation?> AttachedToAsync(SubjectId invitee, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the invitations that still stand over one mailbox's reservation.
    /// </summary>
    /// <param name="mailbox">Which mailbox.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The invitations, neither revoked nor acknowledged, expired or not.</returns>
    ValueTask<IReadOnlyList<Invitation>> ReservingAsync(MailboxId mailbox, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a newly issued invitation onto the transaction in progress.
    /// </summary>
    /// <param name="invitation">The invitation.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask AddAsync(Invitation invitation, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the invitation as it now stands onto its row, forgetting what it bound
    /// where it no longer keeps it.
    /// </summary>
    /// <param name="invitation">The invitation as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(Invitation invitation, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets what every invitation that expired unused bound, keeping its row.
    /// </summary>
    /// <param name="now">The instant to judge them at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were swept.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
