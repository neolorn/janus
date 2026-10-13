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
internal sealed class AccountStore(StoreContext context) : IAccountStore
{
    /// <inheritdoc/>
    public async ValueTask<Account?> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        AccountRecord? record = await FindAsync(subject, cancellationToken).ConfigureAwait(false);

        return record is null ? null : Existing(record);
    }

    /// <inheritdoc/>
    public async ValueTask<Account?> HoldAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        AccountRecord? record = await HeldAsync(context, subject, cancellationToken).ConfigureAwait(false);

        return record is null ? null : Existing(record);
    }

    /// <summary>
    /// The <c>accounts</c> row, read under a lock held until the operation's transaction
    /// ends. A row the context already tracks was read before the lock, so it is read
    /// again: what the operation decides on is the row committed when the lock was taken.
    /// </summary>
    /// <param name="context">The context the operation's writes are tracked on.</param>
    /// <param name="subject">Whose row.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The row, tracked, or nothing where the subject has none.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    /// <remarks>
    /// Implements IDN-LIFE-003: of a takedown's reversal and the erasure at its window's
    /// end, each reads the account here, so the second waits for the first and decides
    /// on what it committed.
    /// </remarks>
    internal static async ValueTask<AccountRecord?> HeldAsync(
        StoreContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An account row is held only inside the operation's transaction.");
        }

        bool tracked = context.Accounts.Local.Any(record => record.Subject == subject);

        AccountRecord? held = (await context.Accounts
                .FromSql($"SELECT * FROM identity.accounts WHERE subject = {subject.Value} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        if (held is not null && tracked)
        {
            await context.Entry(held).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return held;
    }

    /// <summary>
    /// The state of the <c>accounts</c> row, read under a shared lock held until the
    /// operation's transaction ends: a transaction that changes the row waits for this
    /// one, readers under the same lock do not wait for each other, and a transaction
    /// that already holds the row for a change of its own reads it without waiting.
    /// </summary>
    /// <param name="context">The context the operation's writes are tracked on.</param>
    /// <param name="subject">Whose row.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The state as committed when the lock was taken, or as this transaction has
    /// written it, or nothing where the subject has no row.
    /// </returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    /// <remarks>
    /// Implements AUTHZ-GATE-006 (D-183) and CONV-DESIGN-003: the gate judges a
    /// restriction on what is read here, and a restriction takes the row through
    /// <see cref="HeldAsync"/>, so it commits before this read or after the transaction
    /// that made it. The row is read untracked, so nothing the context tracks changes.
    /// </remarks>
    internal static async ValueTask<AccountState?> SharedAsync(
        StoreContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An account row is held only inside the operation's transaction.");
        }

        // A change this transaction tracks and has not flushed is not what the row
        // holds, and the answer is the row's, so the tracked row is not consulted.
        return (await context.Accounts
                .FromSql($"SELECT * FROM identity.accounts WHERE subject = {subject.Value} FOR SHARE")
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault()
            ?.State;
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
                    SuspensionHeld = account.SuspensionHeld,
                    RestrictionHeld = account.RestrictionHeld,
                    DeletingBy = account.DeletingBy,
                    DeletingSince = account.DeletingSince,
                    DeletionHeld = account.DeletionHeld,
                    DeletionHeldSince = account.DeletionHeldSince,
                    AdultAffirmed = account.Registration?.AdultAffirmed,
                    AgeGroup = account.Registration?.Group,
                    AnsweredAgeAt = account.Registration?.AnsweredAgeAt,
                    TermsVersion = account.Registration?.TermsVersion,
                    NoticeVersion = account.Registration?.NoticeVersion,
                    IsEmergency = account.IsEmergency,
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
        record.SuspensionHeld = account.SuspensionHeld;
        record.RestrictionHeld = account.RestrictionHeld;
        record.DeletingBy = account.DeletingBy;
        record.DeletingSince = account.DeletingSince;
        record.DeletionHeld = account.DeletionHeld;
        record.DeletionHeldSince = account.DeletionHeldSince;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Account>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken)
    {
        List<AccountRecord> records = await context.Accounts
            .Where(record =>
                record.State == AccountState.Deleting
                && (record.DeletingSince <= before || record.DeletionHeldSince <= before))
            .OrderBy(record => record.DeletingSince)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(Existing)];
    }

    private static Account Existing(AccountRecord record) =>
        Account.Existing(
            record.Subject,
            record.CreatedAt,
            record.State,
            record.SuspendedBy,
            record.SuspensionHeld,
            record.RestrictionHeld,
            record.DeletingBy,
            record.DeletingSince,
            record.DeletionHeld,
            record.DeletionHeldSince,
            Registered(record),
            record.IsEmergency);

    private static AccountRegistration? Registered(AccountRecord record) =>
        record.AnsweredAgeAt is DateTimeOffset answered
            ? new AccountRegistration(
                record.AdultAffirmed,
                record.AgeGroup,
                answered,
                record.TermsVersion,
                record.NoticeVersion)
            : null;

    private async ValueTask<AccountRecord?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.Accounts.FindAsync([subject], cancellationToken).ConfigureAwait(false);
}
