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
