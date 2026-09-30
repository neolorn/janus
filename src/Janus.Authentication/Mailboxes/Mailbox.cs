using System;
using Janus.Core;

namespace Janus.Authentication.Mailboxes;

/// <summary>
/// One mailbox of the administrative organization: the address it was reserved under,
/// who holds it once a membership attaches, and how far the mail server has been told
/// the state it is owed.
/// </summary>
/// <remarks>
/// Implements INT-MAIL-006, INT-MAIL-006a, INT-MAIL-007 and REG-MAIL-003. One row per
/// address, for good: a retired address reserved again for a later invitation is the
/// same mailbox. The row is the retried outbox of its own pushes. The state it is owed
/// is never stored: it follows from whether the reservation was given up, and from
/// whether the holder is an active account with a current membership, so a suspension
/// committed anywhere is what the next pass pushes, and the last state owed is the one
/// the server ends in. Nothing here removes a mailbox anyone has held. A retired
/// mailbox keeps its last holder until it is reserved again, so an erasure of that
/// holder reaches the address it held. A push marked failed is begun again under the
/// same key a day after it was last marked failed, for as long as its state is owed
/// (D-177).
/// </remarks>
internal sealed class Mailbox
{
    // INT-MAIL-007, D-177: how long a push marked failed waits before a fresh run of
    // the retry schedule begins for it.
    private static readonly TimeSpan Resumed = TimeSpan.FromDays(1);

    private Mailbox(MailboxId id, EmailAddress address, DateTimeOffset reservedAt)
    {
        Id = id;
        Address = address;
        ReservedAt = reservedAt;
    }

    /// <summary>
    /// The mailbox's own identifier.
    /// </summary>
    public MailboxId Id { get; }

    /// <summary>
    /// Its address.
    /// </summary>
    public EmailAddress Address { get; }

    /// <summary>
    /// When it was reserved.
    /// </summary>
    public DateTimeOffset ReservedAt { get; }

    /// <summary>
    /// Whose it is once a membership has attached, and whose it was after the
    /// membership ended until the address is reserved again.
    /// </summary>
    public SubjectId? Holder { get; private set; }

    /// <summary>
    /// When a holder's membership last ended, where one has: the mark of a mailbox
    /// someone has held.
    /// </summary>
    public DateTimeOffset? RetiredAt { get; private set; }

    /// <summary>
    /// When the reservation was given up, where it was before anyone held it.
    /// </summary>
    public DateTimeOffset? ReleasedAt { get; private set; }

    /// <summary>
    /// Whether giving up its reservation removes it: nobody holds it and nobody ever
    /// has, so no mail anyone received is in it.
    /// </summary>
    public bool IsRemovable => Holder is null && RetiredAt is null && ReleasedAt is null;

    /// <summary>
    /// The state the server last confirmed, where it has confirmed one.
    /// </summary>
    public MailboxState? Pushed { get; private set; }

    /// <summary>
    /// The state being pushed, where a push is outstanding.
    /// </summary>
    public MailboxState? Pending { get; private set; }

    /// <summary>
    /// What the outstanding push is recognised by, the same at every attempt.
    /// </summary>
    public Guid? PendingKey { get; private set; }

    /// <summary>
    /// How many attempts the outstanding push has had.
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// When it is next attempted, where an attempt has failed.
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    /// <summary>
    /// When the outstanding push was last marked failed, where it was.
    /// </summary>
    public DateTimeOffset? FailedAt { get; private set; }

    /// <summary>
    /// Whether any push of the mailbox was ever attempted, so the server may hold
    /// something of it.
    /// </summary>
    public bool Attempted { get; private set; }

    /// <summary>
    /// Whether the push outstanding is a removal of a mailbox no push of which was ever
    /// attempted, which the server holds nothing of and which is confirmed unsent.
    /// </summary>
    public bool IsUnsent => Pending is MailboxState.Removed && !Attempted;

    /// <summary>
    /// A mailbox reserved for an address, disabled until a membership attaches.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="at">When it is reserved.</param>
    /// <returns>The mailbox, owed a push that creates it disabled.</returns>
    public static Mailbox Reserved(EmailAddress address, DateTimeOffset at) =>
        new(MailboxId.Of(at), address, at);

    /// <summary>
    /// The mailbox as it already stands. This is the store's translation of a stored
    /// row and no change anyone made.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="address">The address.</param>
    /// <param name="reservedAt">When it was reserved.</param>
    /// <param name="holder">Whose it is or was, where anyone's.</param>
    /// <param name="retiredAt">When a holder's membership last ended, where one has.</param>
    /// <param name="releasedAt">When the reservation was given up, where it was.</param>
    /// <param name="pushed">The state last confirmed, where one was.</param>
    /// <param name="pending">The state being pushed, where one is.</param>
    /// <param name="pendingKey">What that push is recognised by.</param>
    /// <param name="attempts">How many attempts it has had.</param>
    /// <param name="nextAttemptAt">When it is next attempted.</param>
    /// <param name="failedAt">When it was last marked failed.</param>
    /// <param name="attempted">Whether any push of it was ever attempted.</param>
    /// <returns>The mailbox.</returns>
    public static Mailbox Existing(
        MailboxId id,
        EmailAddress address,
        DateTimeOffset reservedAt,
        SubjectId? holder,
        DateTimeOffset? retiredAt,
        DateTimeOffset? releasedAt,
        MailboxState? pushed,
        MailboxState? pending,
        Guid? pendingKey,
        int attempts,
        DateTimeOffset? nextAttemptAt,
        DateTimeOffset? failedAt,
        bool attempted) =>
        new(id, address, reservedAt)
        {
            Holder = holder,
            RetiredAt = retiredAt,
            ReleasedAt = releasedAt,
            Pushed = pushed,
            Pending = pending,
            PendingKey = pendingKey,
            Attempts = attempts,
            NextAttemptAt = nextAttemptAt,
            FailedAt = failedAt,
            Attempted = attempted,
        };

    /// <summary>
    /// Whether an account holds it now.
    /// </summary>
    public bool IsHeld => Holder is not null && RetiredAt is null;

    /// <summary>
    /// Reserves the address again for a later invitation. A mailbox whose reservation
    /// was given up is owed its creation again; a retired one stays disabled and is
    /// no longer its last holder's.
    /// </summary>
    /// <exception cref="InvalidOperationException">An account holds it.</exception>
    public void Reserve()
    {
        if (IsHeld)
        {
            throw new InvalidOperationException("An account holds the mailbox.");
        }

        Holder = null;
        ReleasedAt = null;
    }

    /// <summary>
    /// Gives the mailbox to the account whose membership attached.
    /// </summary>
    /// <param name="holder">Whose it becomes.</param>
    /// <exception cref="InvalidOperationException">
    /// The mailbox is not reserved: someone holds or last held it, or the reservation
    /// was given up.
    /// </exception>
    public void Hold(SubjectId holder)
    {
        if (Holder is not null || ReleasedAt is not null)
        {
            throw new InvalidOperationException("The mailbox is not reserved for anyone to take.");
        }

        Holder = holder;
        RetiredAt = null;
    }

    /// <summary>
    /// Ends the holder's use of the mailbox when the membership ends. It stays,
    /// disabled, and the address is free for a later invitation.
    /// </summary>
    /// <param name="at">When the membership ended.</param>
    /// <exception cref="InvalidOperationException">Nobody holds it.</exception>
    public void Retire(DateTimeOffset at)
    {
        if (!IsHeld)
        {
            throw new InvalidOperationException("Nobody holds the mailbox.");
        }

        RetiredAt = at;
    }

    /// <summary>
    /// Gives up a reservation nobody ever took, which removes the mailbox.
    /// </summary>
    /// <param name="at">When.</param>
    /// <exception cref="InvalidOperationException">The mailbox is not removable.</exception>
    public void Release(DateTimeOffset at)
    {
        if (!IsRemovable)
        {
            throw new InvalidOperationException("The mailbox is not a reservation to give up.");
        }

        ReleasedAt = at;
    }

    /// <summary>
    /// The state the mailbox is owed.
    /// </summary>
    /// <param name="stands">
    /// Whether its holder is an active account with a current membership of the
    /// administrative organization.
    /// </param>
    /// <returns>The state.</returns>
    public MailboxState Owed(bool stands) =>
        ReleasedAt is not null ? MailboxState.Removed
        : IsHeld && stands ? MailboxState.Enabled
        : MailboxState.Disabled;

    /// <summary>
    /// Whether the server holds what the mailbox is owed and nothing is outstanding.
    /// </summary>
    /// <param name="stands">Whether its holder stands.</param>
    /// <returns>Whether there is nothing to do.</returns>
    public bool IsSettled(bool stands) => Pending is null && Pushed == Owed(stands);

    /// <summary>
    /// The push the mailbox is owed now. A change of the state owed begins a push of
    /// its own under a new key and a fresh budget; the same state keeps its key. An
    /// outstanding push may have reached the server though its answer did not, so a
    /// return to the state last confirmed while one is outstanding is pushed again
    /// under a key of its own, never assumed. A push marked failed is begun again under
    /// its key, with a fresh budget, a day after it was last marked failed.
    /// </summary>
    /// <param name="stands">Whether its holder stands.</param>
    /// <param name="now">The instant of the pass.</param>
    /// <returns>
    /// The push, or nothing where the server already holds the state, the next attempt
    /// is not yet due, or the push was marked failed less than a day ago.
    /// </returns>
    public MailboxPush? Due(bool stands, DateTimeOffset now)
    {
        MailboxState owed = Owed(stands);

        if (Pending is null && Pushed == owed)
        {
            return null;
        }

        if (Pending != owed || PendingKey is null)
        {
            Pending = owed;
            PendingKey = Guid.CreateVersion7(now);
            Attempts = 0;
            NextAttemptAt = null;
            FailedAt = null;
        }

        if (FailedAt is DateTimeOffset failed && failed + Resumed <= now)
        {
            Attempts = 0;
            NextAttemptAt = null;
            FailedAt = null;
        }

        return FailedAt is null && (NextAttemptAt is null || NextAttemptAt <= now)
            ? new MailboxPush(Id, PendingKey.Value, Address.Value, owed)
            : null;
    }

    /// <summary>
    /// Records that the server holds the state being pushed.
    /// </summary>
    /// <exception cref="InvalidOperationException">No push is outstanding.</exception>
    public void Confirmed()
    {
        Pushed = Pending ?? throw new InvalidOperationException("No push is outstanding.");

        Clear();
    }

    /// <summary>
    /// Counts an attempt of the outstanding push, as it is about to be made, and marks
    /// the mailbox as one the server may hold something of.
    /// </summary>
    /// <exception cref="InvalidOperationException">No push is outstanding.</exception>
    public void Attempting()
    {
        if (Pending is null)
        {
            throw new InvalidOperationException("No push is outstanding.");
        }

        Attempts++;
        Attempted = true;
    }

    /// <summary>
    /// Marks the outstanding push failed at this attempt, as a conflict retrying cannot
    /// resolve does (D-177).
    /// </summary>
    /// <param name="at">When the attempt was made.</param>
    /// <exception cref="InvalidOperationException">No push is outstanding.</exception>
    public void Failed(DateTimeOffset at)
    {
        if (Pending is null)
        {
            throw new InvalidOperationException("No push is outstanding.");
        }

        NextAttemptAt = null;
        FailedAt = at;
    }

    /// <summary>
    /// Schedules the next attempt after a failed one, the delay growing by the factor
    /// per attempt with full jitter, until the attempts counted spend the budget.
    /// </summary>
    /// <param name="at">When the attempt was made.</param>
    /// <param name="initial">The first retry delay.</param>
    /// <param name="factor">The multiplier per further attempt.</param>
    /// <param name="maximum">How many attempts the budget holds.</param>
    /// <param name="jitter">A fraction of the computed delay, in [0, 1].</param>
    /// <returns>Whether this attempt spent the budget.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The jitter is outside its range.</exception>
    /// <exception cref="InvalidOperationException">No attempt was counted.</exception>
    public bool Refused(DateTimeOffset at, TimeSpan initial, decimal factor, int maximum, double jitter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(jitter);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jitter, 1);

        if (Attempts is 0)
        {
            throw new InvalidOperationException("No attempt of the push was counted.");
        }

        if (Attempts >= maximum)
        {
            NextAttemptAt = null;
            FailedAt = at;

            return true;
        }

        double backoff = initial.TotalSeconds * Math.Pow((double)factor, Attempts - 1);

        NextAttemptAt = at.AddSeconds(backoff * jitter);

        return false;
    }

    private void Clear()
    {
        Pending = null;
        PendingKey = null;
        Attempts = 0;
        NextAttemptAt = null;
        FailedAt = null;
    }
}
