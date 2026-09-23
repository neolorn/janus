using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Mailboxes;
using Janus.Core;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// The mailboxes held in memory, with whether each holder stands set by the test the
/// way an account's state and memberships would decide it.
/// </summary>
internal sealed class MailboxStoreInMemory : IMailboxStore
{
    /// <summary>
    /// Every mailbox, as the rows hold them.
    /// </summary>
    public List<Mailbox> Held { get; } = [];

    /// <summary>
    /// The holders that stand: active accounts with a current membership of the
    /// administrative organization.
    /// </summary>
    public HashSet<SubjectId> Standing { get; } = [];

    /// <summary>
    /// How many times a change was carried onto a row.
    /// </summary>
    public int Recorded { get; private set; }

    /// <summary>
    /// The outstanding push's key each change carried onto the row, in order.
    /// </summary>
    public List<Guid?> Keys { get; } = [];

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<MailboxStanding>> AllAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<MailboxStanding>>(
        [
            .. Held
                .Where(mailbox => mailbox.ReleasedAt is null || mailbox.Pushed is not MailboxState.Removed)
                .OrderBy(mailbox => mailbox.ReservedAt)
                .Select(mailbox => new MailboxStanding(
                    mailbox,
                    mailbox.Holder is SubjectId holder && Standing.Contains(holder))),
        ]);

    /// <inheritdoc/>
    public ValueTask AddAsync(Mailbox mailbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mailbox);

        if (Held.Any(held => held.Address == mailbox.Address))
        {
            throw new InvalidOperationException("The address already has a mailbox.");
        }

        Held.Add(mailbox);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(Mailbox mailbox, CancellationToken cancellationToken)
    {
        if (!Held.Contains(mailbox))
        {
            throw new InvalidOperationException("The mailbox has no row to carry the change.");
        }

        Recorded++;
        Keys.Add(mailbox.PendingKey);

        return ValueTask.CompletedTask;
    }
}
