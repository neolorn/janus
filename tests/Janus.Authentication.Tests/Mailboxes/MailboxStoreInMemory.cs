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
    private readonly Dictionary<MailboxId, DateTimeOffset> _claims = [];

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
    /// The holders erased, whose mailboxes are no longer read, as the store leaves them
    /// once their key is gone.
    /// </summary>
    public HashSet<SubjectId> Erased { get; } = [];

    /// <summary>
    /// How many times a change was carried onto a row.
    /// </summary>
    public int Recorded { get; private set; }

    /// <summary>
    /// The outstanding push's key each change carried onto the row, in order.
    /// </summary>
    public List<Guid?> Keys { get; } = [];

    /// <summary>
    /// The mailboxes a claim is held on, each with when that claim times out.
    /// </summary>
    public IReadOnlyDictionary<MailboxId, DateTimeOffset> Claims => _claims;

    /// <summary>
    /// Stands in for another pass that takes the row over, as one would once the claim
    /// on it had timed out.
    /// </summary>
    /// <param name="mailbox">The row.</param>
    /// <param name="until">When the other pass's claim times out.</param>
    public void TakeOver(MailboxId mailbox, DateTimeOffset until) => _claims[mailbox] = until;

    // A mailbox whose holder was erased has nothing left that reads its address, and
    // one whose removal the server confirmed is a mailbox no more.
    private IEnumerable<Mailbox> Readable =>
        Held
            .Where(mailbox => mailbox.StandsForAddress || mailbox.Pushed is not MailboxState.Removed)
            .Where(mailbox => mailbox.Holder is not SubjectId holder || !Erased.Contains(holder));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<MailboxStanding>> AllAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<MailboxStanding>>(
        [
            .. Readable
                .OrderBy(mailbox => mailbox.ReservedAt)
                .Select(mailbox => new MailboxStanding(
                    mailbox,
                    mailbox.Holder is SubjectId holder && Standing.Contains(holder))),
        ]);

    /// <inheritdoc/>
    public ValueTask<Mailbox?> FindAsync(EmailAddress address, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Readable.FirstOrDefault(mailbox => mailbox.Address == address && mailbox.StandsForAddress));

    /// <inheritdoc/>
    public ValueTask<Mailbox?> FindAsync(MailboxId id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Readable.FirstOrDefault(mailbox => mailbox.Id == id));

    /// <inheritdoc/>
    public ValueTask<Mailbox?> HeldByAsync(SubjectId holder, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Readable.SingleOrDefault(mailbox => mailbox.IsHeld && mailbox.Holder == holder));

    /// <inheritdoc/>
    public ValueTask AddAsync(Mailbox mailbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mailbox);

        if (Readable.Any(held => held.Address == mailbox.Address && held.StandsForAddress))
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

    /// <inheritdoc/>
    public ValueTask<DateTimeOffset?> ClaimAsync(
        MailboxId mailbox,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!Held.Exists(held => held.Id == mailbox)
            || (_claims.TryGetValue(mailbox, out DateTimeOffset until) && until > now))
        {
            return ValueTask.FromResult<DateTimeOffset?>(null);
        }

        _claims[mailbox] = now + timeout;

        return ValueTask.FromResult<DateTimeOffset?>(now + timeout);
    }

    /// <inheritdoc/>
    public ValueTask<MailboxStanding?> StandingAsync(MailboxId mailbox, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Readable.FirstOrDefault(held => held.Id == mailbox) is Mailbox found
                ? new MailboxStanding(found, found.Holder is SubjectId holder && Standing.Contains(holder))
                : null);

    /// <inheritdoc/>
    public ValueTask<bool> AttemptAsync(Mailbox mailbox, DateTimeOffset claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mailbox);

        if (!Holds(mailbox.Id, claim))
        {
            return ValueTask.FromResult(false);
        }

        Recorded++;
        Keys.Add(mailbox.PendingKey);

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> RecordAsync(Mailbox mailbox, DateTimeOffset claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mailbox);

        if (!Holds(mailbox.Id, claim))
        {
            return ValueTask.FromResult(false);
        }

        Recorded++;
        Keys.Add(mailbox.PendingKey);

        return ValueTask.FromResult(_claims.Remove(mailbox.Id));
    }

    /// <inheritdoc/>
    public ValueTask<bool> ReleaseAsync(MailboxId mailbox, DateTimeOffset claim, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Holds(mailbox, claim) && _claims.Remove(mailbox));

    private bool Holds(MailboxId mailbox, DateTimeOffset claim) =>
        _claims.TryGetValue(mailbox, out DateTimeOffset until) && until == claim;
}
