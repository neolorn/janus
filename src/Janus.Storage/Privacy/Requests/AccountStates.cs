using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Records;
using Janus.Privacy.Erasures;
using Janus.Privacy.Requests;

namespace Janus.Storage.Privacy.Requests;

/// <summary>
/// The account states the privacy area moves an account into, over the accounts of
/// the identity area.
/// </summary>
/// <param name="accounts">Where accounts are read and their transitions carried.</param>
/// <param name="records">Where a person record a request names is restricted, and its deletion read.</param>
/// <param name="sessions">What a restriction or a takedown ends in the transaction that makes it.</param>
/// <param name="events">Where each session a restriction or a takedown ends is announced.</param>
/// <remarks>
/// Implements PRIV-RIGHT-004, IDN-LIFE-003, AUTH-SESS-010, REC-ACC-002, REC-REC-003
/// and CONV-DESIGN-003. The transitions are the account aggregate's and the person
/// record's, so a state this adapter cannot reach from is refused there and answered
/// here as no change.
/// </remarks>
internal sealed class AccountStates(
    IAccountStore accounts,
    IPersonRecordStore records,
    ISessionStore sessions,
    IEvents events)
    : IAccountStates
{
    /// <inheritdoc/>
    public async ValueTask HoldAsync(SubjectId subject, CancellationToken cancellationToken) =>
        _ = await accounts.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<bool> RestrictAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // CONV-DESIGN-003: each transition here is decided on the row under its lock,
        // as the reversal of a takedown is, so two at once end as one after the other.
        Account? account = await accounts.HoldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        switch (account)
        {
            // AUTH-SESS-010, IDN-ACCT-007: the sessions end in the transaction that
            // restricts, so what the account signs in with afterwards is a session of the
            // restricted account.
            case { State: AccountState.Active }:
                account.Restrict();

                // Chapter 10 section 5.39 (D-219): no person restricts the account here,
                // so each end is announced as account and names no actor.
                if (await SessionEnds
                        .AnnouncedAsync(
                            events,
                            await sessions.EndAccountAsync(subject, at, cancellationToken).ConfigureAwait(false),
                            SessionEndReason.Account,
                            at,
                            actor: null,
                            cancellationToken)
                        .ConfigureAwait(false) is Error unannounced)
                {
                    throw new InvalidOperationException(unannounced.Code.ToString());
                }

                break;

            // PRIV-RIGHT-004: an account away from active holds the restriction, so it
            // comes back restricted and nothing acts on it meanwhile.
            case { State: AccountState.Suspended or AccountState.Deleting, RestrictionHeld: false }:
                account.HoldRestriction();

                break;

            // REC-ACC-002: a person record's restriction request is fulfilled on the
            // record, which has no session to end; one in its deletion window holds it.
            case null:
                return await RecordRestrictedAsync(subject, cancellationToken).ConfigureAwait(false);

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
        // IDN-LIFE-003: a suspended account begins its deletion only on a request that
        // arrived out of band, holding the suspension.
        Account? account = await accounts.HoldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        // REC-ADM-002: a person record's out-of-band erasure begins its window as an
        // active or restricted account's does, holding a restriction in force.
        if (account is null)
        {
            return await RecordDeletionBegunAsync(subject, origin, at, cancellationToken).ConfigureAwait(false);
        }

        if (!(account.State is AccountState.Active or AccountState.Restricted
                || (account.State is AccountState.Suspended && origin is DeletionOrigin.OutOfBandRequest)))
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

        // REC-REC-003: a person record's window closes into the same erasure.
        IReadOnlyList<PersonRecord> erasing = await records
            .DeletingSinceAsync(before, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. deleting
                .Where(account => account.DeletingBy is not null && account.DeletingSince is not null)
                .Select(account => new PendingDeletion(
                    account.Subject,
                    account.DeletingBy!.Value,
                    account.DeletingSince!.Value,
                    account.DeletionHeldSince)),
            .. erasing
                .Where(record => record.DeletingBy is not null && record.DeletingSince is not null)
                .Select(record => new PendingDeletion(
                    record.Subject,
                    record.DeletingBy!.Value,
                    record.DeletingSince!.Value,
                    record.DeletionHeldSince)
                {
                    IsPersonRecord = true,
                }),
        ];
    }

    /// <inheritdoc/>
    public async ValueTask<AccountStanding?> StandingAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (await accounts.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            is Account account)
        {
            return new AccountStanding(
                account.State,
                account.DeletingBy,
                account.DeletingSince,
                account.DeletionHeldSince,
                account.IsEmergency);
        }

        // REC-ACC-002: a person record bears its subject as an account does, its states
        // spelled as the account's, so a request for it is entered and fulfilled alike.
        return await records.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            is PersonRecord record
            ? new AccountStanding(
                record.State switch
                {
                    PersonRecordState.Active => AccountState.Active,
                    PersonRecordState.Restricted => AccountState.Restricted,
                    PersonRecordState.Deleting => AccountState.Deleting,
                    _ => AccountState.Deleted,
                },
                record.DeletingBy,
                record.DeletingSince,
                record.DeletionHeldSince,
                Emergency: false)
            {
                IsPersonRecord = true,
                AttachedTo = record.AttachedTo,
            }
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TakeDownAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // IDN-LIFE-003: a takedown finds an account in any state but a takedown of its
        // own and an erasure, and holds the one it found.
        if (await accounts.HoldAsync(subject, cancellationToken).ConfigureAwait(false)
            is not { IsEmergency: false } account
            || account is { State: AccountState.Deleted }
            or { State: AccountState.Deleting, DeletingBy: DeletionOrigin.Takedown })
        {
            return false;
        }

        account.Takedown(at);

        await accounts.RecordTransitionAsync(account, cancellationToken).ConfigureAwait(false);

        // Chapter 10 section 5b (D-218): no person takes an account down here, so no actor
        // is named; an event not taken fails the takedown, as its unit of work failing does.
        if (await SessionEnds
                .AnnouncedAsync(
                    events,
                    await sessions.EndAccountAsync(subject, at, cancellationToken).ConfigureAwait(false),
                    SessionEndReason.Account,
                    at,
                    actor: null,
                    cancellationToken)
                .ConfigureAwait(false) is Error unannounced)
        {
            throw new InvalidOperationException(unannounced.Code.ToString());
        }

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ReverseTakedownAsync(
        SubjectId subject,
        DateTimeOffset now,
        DeletionWindows windows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(windows);

        // IDN-LIFE-003: the window is judged again under the lock on the row, so of a
        // reversal and the erasure at the window's end only one commits.
        if (await accounts.HoldAsync(subject, cancellationToken).ConfigureAwait(false)
                is not
                {
                    State: AccountState.Deleting,
                    DeletingBy: DeletionOrigin.Takedown,
                    DeletingSince: DateTimeOffset since,
                } account
            || now >= windows.ErasureDue(DeletionOrigin.Takedown, since, account.DeletionHeldSince))
        {
            return false;
        }

        account.ReverseTakedown();

        await accounts.RecordTransitionAsync(account, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async ValueTask<bool> RecordRestrictedAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        PersonRecord? record = await records.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        switch (record)
        {
            case { State: PersonRecordState.Active }:
                record.Restrict();

                break;

            case { State: PersonRecordState.Deleting, RestrictionHeld: false }:
                record.HoldRestriction();

                break;

            default:
                return false;
        }

        await records.RecordAsync(record, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async ValueTask<bool> RecordDeletionBegunAsync(
        SubjectId subject,
        DeletionOrigin origin,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (await records.HoldAsync(subject, cancellationToken).ConfigureAwait(false)
            is not { State: PersonRecordState.Active or PersonRecordState.Restricted } record)
        {
            return false;
        }

        record.RequestDeletion(origin, at);

        await records.RecordAsync(record, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
