using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Privacy.Erasures;
using Janus.Privacy.Requests;

namespace Janus.Storage.Privacy.Requests;

/// <summary>
/// The account states the privacy area moves an account into, over the accounts of
/// the identity area.
/// </summary>
/// <param name="accounts">Where accounts are read and their transitions carried.</param>
/// <param name="sessions">What a takedown ends in the transaction that makes it.</param>
/// <remarks>
/// Implements PRIV-RIGHT-004, IDN-LIFE-003, AUTH-SESS-010 and CONV-DESIGN-003. The
/// transitions are the account aggregate's, so a state this adapter cannot reach from
/// is refused there and answered here as no change.
/// </remarks>
internal sealed class AccountStates(IAccountStore accounts, ISessionStore sessions) : IAccountStates
{
    /// <inheritdoc/>
    public async ValueTask<bool> RestrictAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        Account? account = await accounts.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        switch (account)
        {
            case { State: AccountState.Active }:
                account.Restrict();

                break;

            // PRIV-RIGHT-004: an account away from active holds the restriction, so it
            // comes back restricted and nothing acts on it meanwhile.
            case { State: AccountState.Suspended or AccountState.Deleting, RestrictionHeld: false }:
                account.HoldRestriction();

                break;

            default:
                return false;
        }

        await accounts.RecordTransitionAsync(account, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> BeginDeletionAsync(
        SubjectId subject,
        DeletionOrigin origin,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (await accounts.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            is not { State: AccountState.Active or AccountState.Restricted } account)
        {
            return false;
        }

        account.RequestDeletion(origin, at);

        await accounts.RecordTransitionAsync(account, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PendingDeletion>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Account> deleting = await accounts
            .DeletingSinceAsync(before, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. deleting
                .Where(account => account.DeletingBy is not null && account.DeletingSince is not null)
                .Select(account => new PendingDeletion(
                    account.Subject,
                    account.DeletingBy!.Value,
                    account.DeletingSince!.Value)),
        ];
    }

    /// <inheritdoc/>
    public async ValueTask<AccountStanding?> StandingAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await accounts.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            is Account account
            ? new AccountStanding(account.State, account.DeletingBy, account.DeletingSince)
            : null;

    /// <inheritdoc/>
    public async ValueTask<bool> TakeDownAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (await accounts.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            is not
            {
                State: AccountState.Active or AccountState.Restricted or AccountState.Suspended,
                IsEmergency: false,
            } account)
        {
            return false;
        }

        account.Takedown(at);

        await accounts.RecordTransitionAsync(account, cancellationToken).ConfigureAwait(false);
        await sessions.EndAccountAsync(subject, at, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ReverseTakedownAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (await accounts.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            is not { State: AccountState.Deleting, DeletingBy: DeletionOrigin.Takedown } account)
        {
            return false;
        }

        account.ReverseTakedown();

        await accounts.RecordTransitionAsync(account, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
