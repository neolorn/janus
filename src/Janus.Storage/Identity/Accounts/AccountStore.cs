using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Accounts;
using Microsoft.EntityFrameworkCore;

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
            record.DeletingSince,
            Registered(record));
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
                    AdultAffirmed = account.Registration?.AdultAffirmed,
                    AgeGroup = account.Registration?.Group,
                    AnsweredAgeAt = account.Registration?.AnsweredAgeAt,
                    TermsVersion = account.Registration?.TermsVersion,
                    NoticeVersion = account.Registration?.NoticeVersion,
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

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Account>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken)
    {
        List<AccountRecord> records = await context.Accounts
            .Where(record =>
                record.State == AccountState.Deleting && record.DeletingSince <= before)
            .OrderBy(record => record.DeletingSince)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. records.Select(record => Account.Existing(
                record.Subject,
                record.CreatedAt,
                record.State,
                record.SuspendedBy,
                record.DeletingBy,
                record.DeletingSince,
                Registered(record))),
        ];
    }

    private static AccountRegistration? Registered(AccountRecord record) =>
        record.AnsweredAgeAt is DateTimeOffset answered
            ? new AccountRegistration(
                record.AdultAffirmed,
                record.AgeGroup,
                answered,
                record.TermsVersion ?? string.Empty,
                record.NoticeVersion ?? string.Empty)
            : null;

    private async ValueTask<AccountRecord?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.Accounts.FindAsync([subject], cancellationToken).ConfigureAwait(false);
}
