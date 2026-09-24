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
        bool restrictionHeld,
        DeletionOrigin? deletingBy,
        DateTimeOffset? deletingSince)
    {
        Subject = subject;
        CreatedAt = createdAt;
        State = state;
        SuspendedBy = suspendedBy;
        RestrictionHeld = restrictionHeld;
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
    /// Whether a restriction of processing is held while the account is away from the
    /// restricted state, so that it is in force again when the account returns
    /// (PRIV-RIGHT-004).
    /// </summary>
    public bool RestrictionHeld { get; private set; }

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
    /// What the registration that created it recorded, and nothing for an account no
    /// registration created.
    /// </summary>
    public AccountRegistration? Registration { get; private init; }

    /// <summary>
    /// Whether it is the reserved <c>emergency</c> account the break-glass session
    /// belongs to, which bootstrap creates and nothing else does. It holds no sign-in
    /// method and is never suspended or deleted (OPS-BOOT-002).
    /// </summary>
    public bool IsEmergency { get; private init; }

    /// <summary>
    /// Creates an account. It is created active, in one transaction, at the end of the
    /// registration session; there is no pending state.
    /// </summary>
    /// <param name="subject">The identifier issued for it.</param>
    /// <param name="createdAt">The instant it was created.</param>
    /// <param name="registration">
    /// What the registration recorded, and nothing where no registration created it.
    /// </param>
    /// <returns>The account.</returns>
    public static Account Create(
        SubjectId subject,
        DateTimeOffset createdAt,
        AccountRegistration? registration = null) =>
        new(subject, createdAt) { Registration = registration };

    /// <summary>
    /// Creates the reserved <c>emergency</c> account, which bootstrap does once.
    /// </summary>
    /// <param name="subject">The identifier issued for it.</param>
    /// <param name="createdAt">The instant it was created.</param>
    /// <returns>The account.</returns>
    public static Account CreateEmergency(SubjectId subject, DateTimeOffset createdAt) =>
        new(subject, createdAt) { IsEmergency = true };

    /// <summary>
    /// The account as it already stands. This is the store's translation of a stored
    /// row and no transition, so it takes the state it is given without asking how the
    /// account reached it.
    /// </summary>
    /// <param name="subject">The identifier the account was issued.</param>
    /// <param name="createdAt">The instant it was created.</param>
    /// <param name="state">The state it is in.</param>
    /// <param name="suspendedBy">Who suspended it, where it is suspended.</param>
    /// <param name="restrictionHeld">
    /// Whether a restriction is held while it is away from the restricted state.
    /// </param>
    /// <param name="deletingBy">Why its grace window began, where one is running.</param>
    /// <param name="deletingSince">When that window began.</param>
    /// <param name="registration">What the registration recorded.</param>
    /// <param name="isEmergency">Whether it is the reserved emergency account.</param>
    /// <returns>The account.</returns>
    public static Account Existing(
        SubjectId subject,
        DateTimeOffset createdAt,
        AccountState state,
        SuspensionOrigin? suspendedBy,
        bool restrictionHeld,
        DeletionOrigin? deletingBy,
        DateTimeOffset? deletingSince,
        AccountRegistration? registration,
        bool isEmergency) =>
        new(subject, createdAt, state, suspendedBy, restrictionHeld, deletingBy, deletingSince)
        {
            Registration = registration,
            IsEmergency = isEmergency,
        };

    /// <summary>
    /// The account's owner deactivates it. Grants are suspended, not removed, and the
    /// owner reactivates it through the link in the deactivation notice or through
    /// ordinary recovery.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not active.</exception>
    public void Deactivate() => EnterSuspension(SuspensionOrigin.Self, AccountState.Active);

    /// <summary>
    /// An administrator suspends the account. Only an administrator reactivates it, so
    /// an account its owner deactivated becomes one the owner can no longer stand back
    /// up, and a restriction in force is remembered to be restored.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The account is neither active, restricted nor deactivated by its owner.
    /// </exception>
    public void Suspend()
    {
        if (State is AccountState.Suspended && SuspendedBy is SuspensionOrigin.Self)
        {
            SuspendedBy = SuspensionOrigin.Administrator;

            return;
        }

        bool restricted = State is AccountState.Restricted;

        EnterSuspension(SuspensionOrigin.Administrator, AccountState.Active, AccountState.Restricted);
        RestrictionHeld = restricted;
    }

    /// <summary>
    /// Restores a suspended account. Prior access returns exactly as it was, a
    /// restriction in force when it was suspended included.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not suspended.</exception>
    public void Reactivate()
    {
        Require(AccountState.Suspended);

        State = RestrictionHeld ? AccountState.Restricted : AccountState.Active;
        SuspendedBy = null;
        RestrictionHeld = false;
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
    /// Restricts processing at the subject's request while the account is suspended or
    /// in its deletion window: the restriction is held, and the account comes back
    /// restricted from either.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The account is neither suspended nor deleting.
    /// </exception>
    public void HoldRestriction()
    {
        Require(AccountState.Suspended, AccountState.Deleting);

        RestrictionHeld = true;
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

        RestrictionHeld = State is AccountState.Restricted;
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
        Unreserved();
        Require(AccountState.Active, AccountState.Restricted, AccountState.Suspended);

        RestrictionHeld = RestrictionHeld || State is AccountState.Restricted;
        State = AccountState.Suspended;
        SuspendedBy = SuspensionOrigin.Administrator;
        EnterDeletion(DeletionOrigin.Takedown, at);
    }

    /// <summary>
    /// Cancels a deletion inside its window and restores the account, restricted where a
    /// restriction is held. A takedown is not cancellable this way.
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
    /// misjudged. What the host did on its <c>TakedownExecuted</c> is not undone; a
    /// restriction held comes back.
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
        RestrictionHeld = false;
    }

    /// <summary>
    /// Records an erasure again after a restore to a point before it took the erasure
    /// away. The account enters the deletion the ledger records from whatever state the
    /// restore left it in, keeping the deletion it was already in, and is erased.
    /// </summary>
    /// <param name="by">The origin the deletion is recorded under where it was not already deleting.</param>
    /// <param name="at">The instant of the erasure, as the ledger records it.</param>
    /// <exception cref="InvalidOperationException">
    /// The account is already deleted, or is the reserved emergency account.
    /// </exception>
    /// <remarks>
    /// Implements DR-016 and DR-006a. The erasure was carried out and reported complete
    /// before the restore, so no state the restore brought back is one it may stay in.
    /// </remarks>
    public void ReapplyErasure(DeletionOrigin by, DateTimeOffset at)
    {
        if (State is not AccountState.Deleting)
        {
            Require(AccountState.Active, AccountState.Restricted, AccountState.Suspended);
            EnterDeletion(by, at);
        }

        MarkErased();
    }

    private void EnterSuspension(SuspensionOrigin by, params AccountState[] from)
    {
        Unreserved();
        Require(from);

        State = AccountState.Suspended;
        SuspendedBy = by;
    }

    private void EnterDeletion(DeletionOrigin by, DateTimeOffset at)
    {
        Unreserved();

        State = AccountState.Deleting;
        DeletingBy = by;
        DeletingSince = at;
    }

    private void LeaveDeletion()
    {
        State = RestrictionHeld ? AccountState.Restricted : AccountState.Active;
        RestrictionHeld = false;
        DeletingBy = null;
        DeletingSince = null;
        SuspendedBy = null;
    }

    // OPS-BOOT-002: the break-glass session's account is the one way in the emergency
    // leaves, so nothing suspends it or starts its deletion.
    private void Unreserved()
    {
        if (IsEmergency)
        {
            throw new InvalidOperationException("The emergency account is never suspended or deleted.");
        }
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
