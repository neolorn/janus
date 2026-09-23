using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Mailboxes;

/// <summary>
/// Where the mailboxes the library provisions are read and written.
/// </summary>
/// <remarks>
/// Implements INT-MAIL-006, INT-MAIL-007 and CONV-DESIGN-003. A mailbox is added and
/// changed inside the transaction that made the change true, so nothing here commits:
/// the caller's unit of work does, and a rollback leaves neither the change nor its
/// push.
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
}
