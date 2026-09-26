using System;
using Janus.Core;
using Janus.Identity.Accounts;
using Xunit;

namespace Janus.Identity.Tests.Accounts;

/// <summary>
/// The one state an account is in and the transitions that move it
/// (IDN-ACCT-007, IDN-LIFE-003, IDN-LIFE-013, IDN-LIFE-014).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccountTests
{
    private static readonly SubjectId Ahmed = new(Guid.Parse("11111111-1111-4111-8111-111111111111"));
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// IDN-ACCT-007 AC1: an account is created in a state, and the state is one of the
    /// five; there is no pending account.
    /// </summary>
    [Fact]
    public void IDN_ACCT_007_AC1_AnAccountIsCreatedActive()
    {
        var account = Account.Create(Ahmed, Noon);

        Assert.Equal(AccountState.Active, account.State);
        Assert.Null(account.SuspendedBy);
        Assert.Null(account.DeletingBy);
    }

    /// <summary>
    /// OPS-BOOT-002: the reserved emergency account is created active and is never
    /// suspended, deactivated, taken down or put into a deletion window.
    /// </summary>
    [Fact]
    public void OPS_BOOT_002_TheEmergencyAccountIsNeverSuspendedOrDeleted()
    {
        var account = Account.CreateEmergency(Ahmed, Noon);

        Assert.True(account.IsEmergency);
        Assert.False(Account.Create(Ahmed, Noon).IsEmergency);
        Assert.Throws<InvalidOperationException>(account.Suspend);
        Assert.Throws<InvalidOperationException>(account.Deactivate);
        Assert.Throws<InvalidOperationException>(() => account.Takedown(Noon));
        Assert.Throws<InvalidOperationException>(() => account.RequestDeletion(DeletionOrigin.Self, Noon));
        Assert.Equal(AccountState.Active, account.State);
    }

    /// <summary>
    /// IDN-ACCT-007 AC4: the grace window is cancellable throughout, and cancelling
    /// restores the account to active.
    /// </summary>
    [Fact]
    public void IDN_ACCT_007_AC4_CancellingADeletionRestoresTheAccount()
    {
        var account = Account.Create(Ahmed, Noon);
        account.RequestDeletion(DeletionOrigin.Self, Noon);

        account.CancelDeletion();

        Assert.Equal(AccountState.Active, account.State);
        Assert.Null(account.DeletingBy);
        Assert.Null(account.DeletingSince);
    }

    /// <summary>
    /// IDN-ACCT-007 AC3: the state reached at the end of the window is deleted, and the
    /// subject identifier still resolves.
    /// </summary>
    [Fact]
    public void IDN_ACCT_007_AC3_ErasureLeavesTheSubjectResolvable()
    {
        var account = Account.Create(Ahmed, Noon);
        account.RequestDeletion(DeletionOrigin.Self, Noon);

        account.MarkErased();

        Assert.Equal(AccountState.Deleted, account.State);
        Assert.Equal(Ahmed, account.Subject);
    }

    /// <summary>
    /// IDN-LIFE-013: the account records who suspended it, because a self-suspended
    /// account is reactivated by its owner and an administrative one is not.
    /// </summary>
    [Theory]
    [InlineData(false, SuspensionOrigin.Self)]
    [InlineData(true, SuspensionOrigin.Administrator)]
    public void Suspend_EitherOrigin_IsRecorded(bool administrative, SuspensionOrigin expected)
    {
        var account = Account.Create(Ahmed, Noon);

        if (administrative)
        {
            account.Suspend();
        }
        else
        {
            account.Deactivate();
        }

        Assert.Equal(AccountState.Suspended, account.State);
        Assert.Equal(expected, account.SuspendedBy);
    }

    /// <summary>
    /// IDN-LIFE-013 AC1: reactivation restores prior access exactly, and leaves no
    /// record of who had suspended it.
    /// </summary>
    [Fact]
    public void IDN_LIFE_013_AC1_ReactivationRestoresTheAccount()
    {
        var account = Account.Create(Ahmed, Noon);
        account.Deactivate();

        account.Reactivate();

        Assert.Equal(AccountState.Active, account.State);
        Assert.Null(account.SuspendedBy);
    }

    /// <summary>
    /// IDN-LIFE-013 AC1 and PRIV-RIGHT-004: an administrator suspending a restricted
    /// account and reactivating it restores the restriction, and an active one comes
    /// back active; an account its owner deactivated becomes the administrator's to
    /// reactivate.
    /// </summary>
    [Fact]
    public void IDN_LIFE_013_AC1_ReactivationRestoresARestrictionInForce()
    {
        var restricted = Account.Create(Ahmed, Noon);
        var active = Account.Create(Ahmed, Noon);
        var deactivated = Account.Create(Ahmed, Noon);

        restricted.Restrict();
        restricted.Suspend();
        active.Suspend();
        deactivated.Deactivate();
        deactivated.Suspend();

        Assert.Equal((AccountState.Suspended, true), (restricted.State, restricted.RestrictionHeld));
        Assert.Equal(SuspensionOrigin.Administrator, deactivated.SuspendedBy);

        restricted.Reactivate();
        active.Reactivate();

        Assert.Equal((AccountState.Restricted, false), (restricted.State, restricted.RestrictionHeld));
        Assert.Equal(AccountState.Active, active.State);
        Assert.Throws<InvalidOperationException>(() => restricted.Reactivate());
    }

    /// <summary>
    /// PRIV-RIGHT-004 AC2: a restricted account keeps its restriction through a deletion
    /// window it leaves and through a takedown that is reversed, and comes back
    /// restricted from both.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_004_AC2_ARestrictionIsHeldThroughADeletionWindow()
    {
        var cancelled = Account.Create(Ahmed, Noon);
        var reversed = Account.Create(Ahmed, Noon);

        cancelled.Restrict();
        cancelled.RequestDeletion(DeletionOrigin.Self, Noon);
        reversed.Restrict();
        reversed.Takedown(Noon);

        Assert.Equal((AccountState.Deleting, true), (cancelled.State, cancelled.RestrictionHeld));
        Assert.Equal((AccountState.Deleting, true), (reversed.State, reversed.RestrictionHeld));

        cancelled.CancelDeletion();
        reversed.ReverseTakedown();

        Assert.Equal((AccountState.Restricted, false), (cancelled.State, cancelled.RestrictionHeld));
        Assert.Equal((AccountState.Restricted, false), (reversed.State, reversed.RestrictionHeld));
    }

    /// <summary>
    /// PRIV-RIGHT-004: a restriction decided while the account is suspended, by either
    /// origin, or in its deletion window is held until it comes back, and an erasure
    /// leaves nothing held; an active account is restricted, never held.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_004_ARestrictionDecidedAwayFromActiveIsHeld()
    {
        var suspended = Account.Create(Ahmed, Noon);
        var deactivated = Account.Create(Ahmed, Noon);
        var erased = Account.Create(Ahmed, Noon);

        suspended.Suspend();
        suspended.HoldRestriction();
        deactivated.Deactivate();
        deactivated.HoldRestriction();
        erased.RequestDeletion(DeletionOrigin.Self, Noon);
        erased.HoldRestriction();

        suspended.Reactivate();
        deactivated.Reactivate();
        erased.MarkErased();

        Assert.Equal(AccountState.Restricted, suspended.State);
        Assert.Equal(AccountState.Restricted, deactivated.State);
        Assert.Equal((AccountState.Deleted, false), (erased.State, erased.RestrictionHeld));
        Assert.Throws<InvalidOperationException>(() => Account.Create(Ahmed, Noon).HoldRestriction());
        Assert.Throws<InvalidOperationException>(erased.HoldRestriction);
    }

    /// <summary>
    /// IDN-LIFE-003 AC4: a takedown passes the account through suspension into the
    /// grace window in one transaction, recording that a takedown put it there.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003_AC4_ATakedownPassesThroughSuspensionIntoTheWindow()
    {
        var account = Account.Create(Ahmed, Noon);

        account.Takedown(Noon);

        Assert.Equal(AccountState.Deleting, account.State);
        Assert.Equal(SuspensionOrigin.Administrator, account.SuspendedBy);
        Assert.Equal(DeletionOrigin.Takedown, account.DeletingBy);
        Assert.Equal(Noon, account.DeletingSince);
    }

    /// <summary>
    /// IDN-LIFE-003 AC5: reversal inside the window restores active; the account signs
    /// in again.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003_AC5_ReversalInsideTheWindowRestoresTheAccount()
    {
        var account = Account.Create(Ahmed, Noon);
        account.Takedown(Noon);

        account.ReverseTakedown();

        Assert.Equal(AccountState.Active, account.State);
        Assert.Null(account.DeletingBy);
        Assert.Null(account.SuspendedBy);
    }

    /// <summary>
    /// IDN-LIFE-003 AC6: a takedown-originated window is not cancelled; the only way
    /// back is the reversal.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003_AC6_ATakedownWindowIsNotCancellable()
    {
        var account = Account.Create(Ahmed, Noon);
        account.Takedown(Noon);

        Assert.Throws<InvalidOperationException>(account.CancelDeletion);
    }

    /// <summary>
    /// IDN-LIFE-003: the ordinary deletion is not reversed through the takedown path
    /// either, so neither operation can stand in for the other.
    /// </summary>
    [Fact]
    public void ReverseTakedown_AnOrdinaryDeletion_Throws()
    {
        var account = Account.Create(Ahmed, Noon);
        account.RequestDeletion(DeletionOrigin.Self, Noon);

        Assert.Throws<InvalidOperationException>(account.ReverseTakedown);
    }

    /// <summary>
    /// IDN-LIFE-003: a takedown is entered through its own operation, so the ordinary
    /// deletion cannot record the takedown origin.
    /// </summary>
    [Fact]
    public void RequestDeletion_TheTakedownOrigin_Throws()
    {
        var account = Account.Create(Ahmed, Noon);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => account.RequestDeletion(DeletionOrigin.Takedown, Noon));
    }

    /// <summary>
    /// IDN-ACCT-007 AC2: restriction leaves the account able to exercise its data
    /// subject rights, of which erasure is one.
    /// </summary>
    [Fact]
    public void RequestDeletion_ARestrictedAccount_EntersTheWindow()
    {
        var account = Account.Create(Ahmed, Noon);
        account.Restrict();

        account.RequestDeletion(DeletionOrigin.OutOfBandRequest, Noon);

        Assert.Equal(AccountState.Deleting, account.State);
        Assert.Equal(DeletionOrigin.OutOfBandRequest, account.DeletingBy);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC3: a restriction request undecided at its deadline moves the
    /// account to restricted, and the restriction is lifted the same way.
    /// </summary>
    [Fact]
    public void Restrict_AnActiveAccount_MovesItAndBack()
    {
        var account = Account.Create(Ahmed, Noon);

        account.Restrict();

        Assert.Equal(AccountState.Restricted, account.State);

        account.LiftRestriction();

        Assert.Equal(AccountState.Active, account.State);
    }

    /// <summary>
    /// IDN-ACCT-007 AC1: a transition the chapters do not state is refused rather than
    /// silently producing a state nobody meant.
    /// </summary>
    [Theory]
    [InlineData(AccountState.Deleted)]
    [InlineData(AccountState.Deleting)]
    public void Transitions_FromAStateThatDoesNotMakeThem_Throw(AccountState state)
    {
        var account = Account.Create(Ahmed, Noon);
        account.RequestDeletion(DeletionOrigin.Self, Noon);

        if (state is AccountState.Deleted)
        {
            account.MarkErased();
        }

        Assert.Throws<InvalidOperationException>(account.Deactivate);
        Assert.Throws<InvalidOperationException>(account.Restrict);
        Assert.Throws<InvalidOperationException>(account.Reactivate);
    }

    /// <summary>
    /// DR-016 AC3, DR-006a AC1: an erasure the ledger records is carried out again from
    /// whatever state a restore left the account in; one that was not deleting enters
    /// the deletion the ledger records, and none holds a restriction once erased.
    /// </summary>
    /// <param name="state">The state the restore left the account in.</param>
    [Theory]
    [InlineData(AccountState.Active)]
    [InlineData(AccountState.Restricted)]
    [InlineData(AccountState.Suspended)]
    public void DR_016_AC3_AnErasureIsReappliedFromTheStateARestoreLeft(AccountState state)
    {
        var account = Account.Create(Ahmed, Noon.AddDays(-30));

        if (state is AccountState.Restricted)
        {
            account.Restrict();
        }
        else if (state is AccountState.Suspended)
        {
            account.Suspend();
        }

        account.ReapplyErasure(DeletionOrigin.OutOfBandRequest, Noon);

        Assert.Equal(AccountState.Deleted, account.State);
        Assert.Equal(DeletionOrigin.OutOfBandRequest, account.DeletingBy);
        Assert.Equal(Noon, account.DeletingSince);
        Assert.False(account.RestrictionHeld);
    }

    /// <summary>
    /// DR-016 AC3: an account the restore left inside its deletion window keeps the
    /// deletion it was in and is erased.
    /// </summary>
    [Fact]
    public void DR_016_AC3_AnAccountLeftDeletingKeepsItsDeletion()
    {
        var account = Account.Create(Ahmed, Noon.AddDays(-30));
        account.RequestDeletion(DeletionOrigin.Self, Noon.AddDays(-1));

        account.ReapplyErasure(DeletionOrigin.OutOfBandRequest, Noon);

        Assert.Equal(AccountState.Deleted, account.State);
        Assert.Equal(DeletionOrigin.Self, account.DeletingBy);
        Assert.Equal(Noon.AddDays(-1), account.DeletingSince);
    }

    /// <summary>
    /// DR-016 AC3, OPS-BOOT-002: an account already erased is not erased again, and the
    /// emergency account is never erased.
    /// </summary>
    [Fact]
    public void DR_016_AC3_AnErasedOrEmergencyAccountIsNotReapplied()
    {
        var erased = Account.Create(Ahmed, Noon);
        erased.RequestDeletion(DeletionOrigin.Self, Noon);
        erased.MarkErased();

        var emergency = Account.CreateEmergency(Ahmed, Noon);

        Assert.Throws<InvalidOperationException>(() => erased.ReapplyErasure(DeletionOrigin.OutOfBandRequest, Noon));
        Assert.Throws<InvalidOperationException>(() => emergency.ReapplyErasure(DeletionOrigin.OutOfBandRequest, Noon));
    }

    /// <summary>
    /// IDN-PRIN-003: an erased account is never erased a second time, so no operation
    /// rewrites a record of something that already happened.
    /// </summary>
    [Fact]
    public void MarkErased_AnAlreadyErasedAccount_Throws()
    {
        var account = Account.Create(Ahmed, Noon);
        account.RequestDeletion(DeletionOrigin.Self, Noon);
        account.MarkErased();

        Assert.Throws<InvalidOperationException>(account.MarkErased);
    }
}
