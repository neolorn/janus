using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Mailboxes;

/// <summary>
/// Where the mailboxes the library provisions are read and written.
/// </summary>
/// <remarks>
/// Implements INT-MAIL-006, INT-MAIL-007 and CONV-DESIGN-003. A mailbox is added and
/// changed inside the transaction that made the change true, so nothing here commits:
/// the caller's unit of work does, and a rollback leaves neither the change nor its
/// push. A push is carried only under a claim on its mailbox's row, and what a pass made
/// of it is written only while that claim stands, so two passes over the same mailboxes
/// carry each push once.
/// </remarks>
internal interface IMailboxStore
{
    /// <summary>
    /// Reads every mailbox that still exists or is still owed its removal, each with
    /// whether its holder stands.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The mailboxes, oldest first.</returns>
    ValueTask<IReadOnlyList<MailboxStanding>> AllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the mailbox of one address, whatever state it is in.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The mailbox, or nothing where the address never had one or its last holder was
    /// erased.
    /// </returns>
    ValueTask<Mailbox?> FindAsync(EmailAddress address, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one mailbox, whatever state it is in.
    /// </summary>
    /// <param name="id">Which mailbox.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The mailbox, or nothing where no such row exists or its last holder was erased.</returns>
    ValueTask<Mailbox?> FindAsync(MailboxId id, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the mailbox an account holds now.
    /// </summary>
    /// <param name="holder">Whose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The mailbox, or nothing where the account holds none.</returns>
    ValueTask<Mailbox?> HeldByAsync(SubjectId holder, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a newly reserved mailbox onto the transaction in progress.
    /// </summary>
    /// <param name="mailbox">The mailbox.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask AddAsync(Mailbox mailbox, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the mailbox as it now stands onto its row.
    /// </summary>
    /// <param name="mailbox">The mailbox as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(Mailbox mailbox, CancellationToken cancellationToken);

    /// <summary>
    /// Claims one mailbox's push for a pass, by one update that succeeds only where the
    /// row is unclaimed or its claim has timed out. The caller commits it on its own
    /// before the mail server is called.
    /// </summary>
    /// <param name="mailbox">Which mailbox.</param>
    /// <param name="now">The instant of the claim.</param>
    /// <param name="timeout">How long the claim stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The instant the claim stands until, which names it, or nothing where another pass
    /// holds the row or the row is gone.
    /// </returns>
    ValueTask<DateTimeOffset?> ClaimAsync(
        MailboxId mailbox,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads one mailbox as its row stands now, with whether its holder stands, which is
    /// what a pass that claimed it decides on.
    /// </summary>
    /// <param name="mailbox">Which mailbox.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// The mailbox, or nothing where no such row exists, its last holder was erased or
    /// the server has confirmed its removal.
    /// </returns>
    ValueTask<MailboxStanding?> StandingAsync(MailboxId mailbox, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the outstanding push and the attempt counted for it onto the row before
    /// the attempt is made, by one update that changes nothing where the claim has been
    /// taken over. The claim stands.
    /// </summary>
    /// <param name="mailbox">The mailbox, its attempt counted.</param>
    /// <param name="claim">The claim the attempt is made under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the attempt was written under the claim.</returns>
    ValueTask<bool> AttemptAsync(Mailbox mailbox, DateTimeOffset claim, CancellationToken cancellationToken);

    /// <summary>
    /// Writes what a pass made of the mailbox's push onto the row and gives the claim
    /// up, by one update that changes nothing where the claim has been taken over.
    /// </summary>
    /// <param name="mailbox">The mailbox as the pass left it.</param>
    /// <param name="claim">The claim the pass held.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the outcome was written under the claim.</returns>
    ValueTask<bool> RecordAsync(Mailbox mailbox, DateTimeOffset claim, CancellationToken cancellationToken);

    /// <summary>
    /// Gives up the claim on a mailbox a pass had nothing to write for, by one update
    /// that changes nothing where the claim has been taken over.
    /// </summary>
    /// <param name="mailbox">Which mailbox.</param>
    /// <param name="claim">The claim the pass held.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the claim was given up.</returns>
    ValueTask<bool> ReleaseAsync(MailboxId mailbox, DateTimeOffset claim, CancellationToken cancellationToken);
}
