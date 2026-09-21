using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
/// <remarks>
/// Implements PRIV-RIGHT-004, IDN-LIFE-003 and CONV-DESIGN-003. The transitions are
/// the account aggregate's, so a state this adapter cannot reach from is refused
/// there and answered here as no change.
/// </remarks>
internal sealed class AccountStates(IAccountStore accounts) : IAccountStates
{
    /// <inheritdoc/>
    public async ValueTask<bool> RestrictAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (await accounts.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            is not { State: AccountState.Active } account)
        {
            return false;
        }

        account.Restrict();

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
}
