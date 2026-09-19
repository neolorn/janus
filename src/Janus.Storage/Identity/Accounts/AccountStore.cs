using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Accounts;

namespace Janus.Storage.Identity.Accounts;

/// <summary>
/// Accounts, over the <c>accounts</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements IDN-ACCT-001 and CONV-DESIGN-003. The translation between the account and
/// its row lives here and nowhere else; the account holds no column and the row holds no
/// transition.
/// </remarks>
internal sealed class AccountStore(JanusDbContext context) : IAccountStore
{
    /// <inheritdoc/>
    public async ValueTask<Account?> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        AccountRecord? record = await FindAsync(subject, cancellationToken).ConfigureAwait(false);

        return record is null ? null : Account.Existing(
            record.Subject,
            record.CreatedAt,
            record.State,
            record.SuspendedBy,
            record.DeletingBy,
            record.DeletingSince);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        await context.Accounts
            .AddAsync(
                new AccountRecord
                {
                    Subject = account.Subject,
                    CreatedAt = account.CreatedAt,
                    State = account.State,
                    SuspendedBy = account.SuspendedBy,
                    DeletingBy = account.DeletingBy,
                    DeletingSince = account.DeletingSince,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordTransitionAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        AccountRecord record = await FindAsync(account.Subject, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The account has no row to carry the transition.");

        record.State = account.State;
        record.SuspendedBy = account.SuspendedBy;
        record.DeletingBy = account.DeletingBy;
        record.DeletingSince = account.DeletingSince;
    }

    private async ValueTask<AccountRecord?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.Accounts.FindAsync([subject], cancellationToken).ConfigureAwait(false);
}
