using System;
using Janus.Core;

namespace Janus.Identity.Accounts;

/// <summary>
/// A person's identity within the deployment. It is the sole source of truth for that
/// identity: an external provider attaches a credential and never holds the record.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-001, IDN-ACCT-002, IDN-ACCT-007, IDN-LIFE-003, IDN-LIFE-013,
/// IDN-LIFE-014 and chapter 10 sections 5.1 and 5.12b. An account is in exactly one
/// state and reaches the next only through the transition named for it; the record is
/// never removed, so every removal is expressed as state and, for personal data, as
/// erasure.
/// </remarks>
internal sealed class Account
{
    private Account(SubjectId subject, DateTimeOffset createdAt)
    {
        Subject = subject;
        CreatedAt = createdAt;
        State = AccountState.Active;
    }

    private Account(
        SubjectId subject,
        DateTimeOffset createdAt,
        AccountState state,
        SuspensionOrigin? suspendedBy,
        DeletionOrigin? deletingBy,
        DateTimeOffset? deletingSince)
    {
        Subject = subject;
        CreatedAt = createdAt;
        State = state;
        SuspendedBy = suspendedBy;
        DeletingBy = deletingBy;
        DeletingSince = deletingSince;
    }

    /// <summary>
    /// The stable opaque identifier audit records, grants and tokens reference.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// When the account was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// The one state the account is in.
    /// </summary>
    public AccountState State { get; private set; }

    /// <summary>
    /// Who suspended the account, where it is suspended. The two origins are reversed
    /// differently.
    /// </summary>
    public SuspensionOrigin? SuspendedBy { get; private set; }

    /// <summary>
    /// Why the account entered its grace window, where it is deleting. The origin
    /// decides what the subject is told and who may cancel.
    /// </summary>
    public DeletionOrigin? DeletingBy { get; private set; }

    /// <summary>
    /// When the grace window began, where it is running.
    /// </summary>
    public DateTimeOffset? DeletingSince { get; private set; }

    /// <summary>
    /// Creates an account. It is created active, in one transaction, at the end of the
    /// registration session; there is no pending state.
    /// </summary>
    /// <param name="subject">The identifier issued for it.</param>
    /// <param name="createdAt">The instant it was created.</param>
    /// <returns>The account.</returns>
    public static Account Create(SubjectId subject, DateTimeOffset createdAt) =>
        new(subject, createdAt);

    /// <summary>
    /// The account as it already stands. This is the store's translation of a stored
    /// row and no transition, so it takes the state it is given without asking how the
    /// account reached it.
    /// </summary>
    /// <param name="subject">The identifier the account was issued.</param>
    /// <param name="createdAt">The instant it was created.</param>
    /// <param name="state">The state it is in.</param>
    /// <param name="suspendedBy">Who suspended it, where it is suspended.</param>
    /// <param name="deletingBy">Why its grace window began, where one is running.</param>
    /// <param name="deletingSince">When that window began.</param>
    /// <returns>The account.</returns>
    public static Account Existing(
        SubjectId subject,
        DateTimeOffset createdAt,
        AccountState state,
        SuspensionOrigin? suspendedBy,
        DeletionOrigin? deletingBy,
        DateTimeOffset? deletingSince) =>
        new(subject, createdAt, state, suspendedBy, deletingBy, deletingSince);

    /// <summary>
    /// The account's owner deactivates it. Grants are suspended, not removed, and the
    /// owner reactivates it through the link in the deactivation notice or through
    /// ordinary recovery.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not active.</exception>
    public void Deactivate() => EnterSuspension(SuspensionOrigin.Self, AccountState.Active);

    /// <summary>
    /// An administrator suspends the account. Only an administrator reactivates it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The account is neither active nor restricted.
    /// </exception>
    public void Suspend() =>
        EnterSuspension(SuspensionOrigin.Administrator, AccountState.Active, AccountState.Restricted);

    /// <summary>
    /// Restores a suspended account. Prior access returns exactly as it was.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not suspended.</exception>
    public void Reactivate()
    {
        Require(AccountState.Suspended);

        State = AccountState.Active;
        SuspendedBy = null;
    }

    /// <summary>
    /// Restricts processing at the subject's request. The account still signs in and
    /// reads its own data; nothing acts on it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not active.</exception>
    public void Restrict()
    {
        Require(AccountState.Active);

        State = AccountState.Restricted;
    }

    /// <summary>
    /// Lifts the restriction.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not restricted.</exception>
    public void LiftRestriction()
    {
        Require(AccountState.Restricted);

        State = AccountState.Active;
    }

    /// <summary>
    /// Starts the deletion grace window. A restricted account may start it too, because
    /// exercising a data subject right is what restriction leaves available.
    /// </summary>
    /// <param name="by">Whether the subject asked or a request arrived out of band.</param>
    /// <param name="at">The instant the window began.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The origin is the takedown's, which enters the window through its own operation.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The account is neither active nor restricted.
    /// </exception>
    public void RequestDeletion(DeletionOrigin by, DateTimeOffset at)
    {
        if (by is DeletionOrigin.Takedown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(by),
                by,
                "A takedown enters the window through its own operation.");
        }

        Require(AccountState.Active, AccountState.Restricted);
        EnterDeletion(by, at);
    }

    /// <summary>
    /// Triggers a takedown: the account passes through suspension into the grace window
    /// in one transaction, and the only way back is the reversal.
    /// </summary>
    /// <param name="at">The instant the takedown was triggered.</param>
    /// <exception cref="InvalidOperationException">
    /// The account is already deleting or deleted.
    /// </exception>
    public void Takedown(DateTimeOffset at)
    {
        Require(AccountState.Active, AccountState.Restricted, AccountState.Suspended);

        State = AccountState.Suspended;
        SuspendedBy = SuspensionOrigin.Administrator;
        EnterDeletion(DeletionOrigin.Takedown, at);
    }

    /// <summary>
    /// Cancels a deletion inside its window and restores the account. A takedown is not
    /// cancellable this way.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The account is not deleting, or its window was entered by a takedown.
    /// </exception>
    public void CancelDeletion()
    {
        Require(AccountState.Deleting);

        if (DeletingBy is DeletionOrigin.Takedown)
        {
            throw new InvalidOperationException("A takedown is reversed, never cancelled.");
        }

        LeaveDeletion();
    }

    /// <summary>
    /// Reverses a takedown inside its window, for the case where an adult was
    /// misjudged. Cancelled orders are not restored.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The account is not deleting, or its window was not entered by a takedown.
    /// </exception>
    public void ReverseTakedown()
    {
        Require(AccountState.Deleting);

        if (DeletingBy is not DeletionOrigin.Takedown)
        {
            throw new InvalidOperationException("Only a takedown is reversed.");
        }

        LeaveDeletion();
    }

    /// <summary>
    /// Records that the erasure transaction has run. The row stays and the identifier
    /// still resolves; the personal fields are unreadable because the key is gone.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not deleting.</exception>
    public void MarkErased()
    {
        Require(AccountState.Deleting);

        State = AccountState.Deleted;
    }

    private void EnterSuspension(SuspensionOrigin by, params AccountState[] from)
    {
        Require(from);

        State = AccountState.Suspended;
        SuspendedBy = by;
    }

    private void EnterDeletion(DeletionOrigin by, DateTimeOffset at)
    {
        State = AccountState.Deleting;
        DeletingBy = by;
        DeletingSince = at;
    }

    private void LeaveDeletion()
    {
        State = AccountState.Active;
        DeletingBy = null;
        DeletingSince = null;
        SuspendedBy = null;
    }

    private void Require(params AccountState[] states)
    {
        if (Array.IndexOf(states, State) < 0)
        {
            throw new InvalidOperationException(
                "An account in " + State + " does not make this transition.");
        }
    }
}
