using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.BreakGlass;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Accounts;

/// <summary>
/// What an administrator does to the standing of someone else's account: suspends it,
/// reactivates it, lifts its restriction and cancels its deletion, and reads the photo it
/// shows.
/// </summary>
/// <param name="scope">Whether the caller administers accounts in the deployment.</param>
/// <param name="stepUp">What the gated operations ask of the administrator's session.</param>
/// <param name="directory">Where the standing is read and the transition carried.</param>
/// <param name="emergency">Which account no administrator suspends.</param>
/// <param name="sessions">What a suspension ends.</param>
/// <param name="links">Where the link a deletion notice carried is held.</param>
/// <param name="configuration">Where the grace window is read.</param>
/// <param name="events">Where the transition is announced.</param>
/// <param name="audit">Where what the administrator did is recorded.</param>
/// <param name="photos">Where the photo an account shows is read.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-LIFE-013, AUTH-SESS-010, PRIV-RIGHT-004, IDN-AUD-001 and chapter 09
/// section 8a. The grants of a suspended account are left in place, which is what lets
/// reactivation restore them exactly; the gate confers nothing on an account that is
/// not active.
/// </remarks>
internal sealed class AccountAdministration(
    AdministrativeScope scope,
    StepUpGuard stepUp,
    IAccountDirectory directory,
    IEmergencyAccount emergency,
    ISessionStore sessions,
    ILifecycleLinkStore links,
    IConfigurationStore configuration,
    IEvents events,
    IAccountAudit audit,
    ProfilePhotos photos,
    IUnitOfWork work,
    TimeProvider time) : IAccounts
{
    /// <inheritdoc/>
    public async ValueTask<Result> SuspendAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        (Error? refusal, AccountState? state) = await SuspendableAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (refusal is not null || state is null)
        {
            return refusal is null ? Result.Success() : Result.Failure(refusal);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.AccountSuspend, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: decided again on the account's row under its lock, so a transition
        // committed since the first decision is the one this one follows.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        (refusal, state) = await SuspendableAsync(subject, cancellationToken).ConfigureAwait(false);

        if (refusal is not null || state is null)
        {
            return await SettledAsync(refusal, cancellationToken).ConfigureAwait(false);
        }

        await directory.SuspendAsync(subject, cancellationToken).ConfigureAwait(false);

        // AUTH-SESS-010: the sessions end in the transaction that suspends, so the
        // first request on any of them after the commit is refused.
        await sessions.EndAccountAsync(subject, now, cancellationToken).ConfigureAwait(false);

        // An account its owner deactivated is already suspended, so taking it over
        // changes who reverses it and announces nothing: the state did not change.
        if (state is not AccountState.Suspended)
        {
            Result published = await events
                .PublishAsync(
                    new AccountSuspended(now, Key(subject, now), SuspensionOrigin.Administrator)
                    {
                        Subject = subject,
                        Actor = acting,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (published.Match(() => (Error?)null, error => error) is Error unpublished)
            {
                return Result.Failure(unpublished);
            }
        }

        await audit
            .AdministeredAsync(AuditActions.AccountSuspended, acting, context.BreakGlassReason, subject, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> ReactivateAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (await ReactivationRefusedAsync(subject, cancellationToken).ConfigureAwait(false) is Error refusal)
        {
            return Result.Failure(refusal);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.AccountReactivate, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: as for a suspension.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (await ReactivationRefusedAsync(subject, cancellationToken).ConfigureAwait(false) is Error moved)
        {
            return await SettledAsync(moved, cancellationToken).ConfigureAwait(false);
        }

        await directory.ReinstateAsync(subject, cancellationToken).ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new AccountReactivated(now, Key(subject, now))
                {
                    Subject = subject,
                    Actor = acting,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure(unpublished);
        }

        await audit
            .AdministeredAsync(AuditActions.AccountReactivated, acting, context.BreakGlassReason, subject, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> LiftRestrictionAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (await LiftRefusedAsync(subject, cancellationToken).ConfigureAwait(false) is Error refusal)
        {
            return Result.Failure(refusal);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.AccountRestrictionLift, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: as for a suspension.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (await LiftRefusedAsync(subject, cancellationToken).ConfigureAwait(false) is Error moved)
        {
            return await SettledAsync(moved, cancellationToken).ConfigureAwait(false);
        }

        await directory.LiftRestrictionAsync(subject, now, cancellationToken).ConfigureAwait(false);
        await audit
            .AdministeredAsync(AuditActions.RestrictionLifted, acting, context.BreakGlassReason, subject, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> CancelDeletionAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        DateTimeOffset now = time.GetUtcNow();

        (Error? refusal, HeldDeletion? deleting) = await CancellableAsync(subject, now, cancellationToken)
            .ConfigureAwait(false);

        if (refusal is not null)
        {
            return Result.Failure(refusal);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.AccountDeletionCancel, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: as for a suspension.
        await directory.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        (refusal, deleting) = await CancellableAsync(subject, now, cancellationToken).ConfigureAwait(false);

        if (refusal is not null || deleting is null)
        {
            return await SettledAsync(refusal, cancellationToken).ConfigureAwait(false);
        }

        // IDN-LIFE-003: the cancellation of a window an out-of-band request began is
        // recorded against that request.
        PrivacyRequestId? request = deleting.By is DeletionOrigin.OutOfBandRequest
            ? await directory.ErasureRequestAsync(subject, deleting.Since, cancellationToken).ConfigureAwait(false)
            : null;

        await directory.CancelDeletionAsync(subject, cancellationToken).ConfigureAwait(false);
        await links.RemoveAsync(subject, cancellationToken).ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new AccountDeletionCancelled(now, Key(subject, now))
                {
                    Subject = subject,
                    Actor = acting,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure(unpublished);
        }

        await audit
            .CancelledOnBehalfAsync(acting, context.BreakGlassReason, subject, request, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result<ReadOnlyMemory<byte>>> ReadPhotoAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.AccountManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<ReadOnlyMemory<byte>>(refused);
        }

        return await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false) is null
            ? Result.Failure<ReadOnlyMemory<byte>>(Error.From(ErrorCodes.AccountNotFound))
            : await photos.ReadOfAsync(subject, cancellationToken).ConfigureAwait(false);
    }

    // What a suspension is refused with, where it is; an account an administrator
    // already suspended is settled with nothing to do, which is a refusal of nothing
    // and no state; otherwise the state it is suspended from.
    private async ValueTask<(Error? Refusal, AccountState? State)> SuspendableAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        AccountState? state = await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false);

        // OPS-BOOT-002: the break-glass session's account is never suspended, since it is
        // the way in an emergency leaves.
        if (state is not null
            && await emergency.FindAsync(cancellationToken).ConfigureAwait(false) == subject)
        {
            return (Error.From(ErrorCodes.Denied), null);
        }

        return state switch
        {
            null => (Error.From(ErrorCodes.AccountNotFound), null),
            AccountState.Deleting or AccountState.Deleted => (StateConflict(state.Value, suspendedBy: null), null),
            AccountState.Suspended
                when await directory.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)
                    is SuspensionOrigin.Administrator => (null, null),
            _ => (null, state),
        };
    }

    private async ValueTask<Error?> ReactivationRefusedAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is not AccountState state)
        {
            return Error.From(ErrorCodes.AccountNotFound);
        }

        // IDN-LIFE-013: the two suspensions are reversed differently, and what its owner
        // deactivated is theirs to stand back up.
        SuspensionOrigin? suspendedBy = state is AccountState.Suspended
            ? await directory.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)
            : null;

        return suspendedBy is SuspensionOrigin.Administrator ? null : StateConflict(state, suspendedBy);
    }

    private async ValueTask<Error?> LiftRefusedAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false) switch
        {
            null => Error.From(ErrorCodes.AccountNotFound),

            // PRIV-RIGHT-004: a restriction held while the account is suspended or
            // deleting stays until the account is back in the restricted state.
            AccountState.Suspended => StateConflict(
                AccountState.Suspended,
                await directory.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)),
            not AccountState.Restricted and AccountState other => StateConflict(other, suspendedBy: null),
            _ => null,
        };

    // What a cancellation is refused with, where it is, or the deletion it cancels.
    private async ValueTask<(Error? Refusal, HeldDeletion? Deleting)> CancellableAsync(
        SubjectId subject,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is not AccountState state)
        {
            return (Error.From(ErrorCodes.AccountNotFound), null);
        }

        if (await directory.DeletingAsync(subject, cancellationToken).ConfigureAwait(false)
            is not HeldDeletion deleting)
        {
            return (StateConflict(
                state,
                state is AccountState.Suspended
                    ? await directory.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)
                    : null), null);
        }

        // IDN-LIFE-003: a takedown is reversed through its own operation, never cancelled.
        if (deleting.By is DeletionOrigin.Takedown)
        {
            return (Error.From(ErrorCodes.TakedownActive), null);
        }

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.AccountDeletionGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        return now >= deleting.Since + grace
            ? (Error.From(ErrorCodes.DeletionWindowElapsed), null)
            : (null, deleting);
    }

    // CONV-DESIGN-003: a decision taken again under the lock that settles the operation
    // before its write commits the transaction with nothing in it, so the unit of work
    // is left clean; a refusal of nothing is the success of an operation already done.
    private async ValueTask<Result> SettledAsync(Error? refusal, CancellationToken cancellationToken) =>
        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => refusal is null ? Result.Success() : Result.Failure(refusal), Result.Failure);

    private static string Key(SubjectId subject, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{subject.Value}@{at.UtcTicks}");

    // Chapter 10 section 1.1: the state is named as section 5.1 spells it and, where it
    // is suspended, who suspended it as section 5.12b does.
    internal static Error StateConflict(AccountState state, SuspensionOrigin? suspendedBy)
    {
        var details = new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
        {
            ["state"] = JsonSerializer.SerializeToElement(WrittenName.Of(state)),
        };

        if (suspendedBy is SuspensionOrigin by)
        {
            details["suspendedBy"] = JsonSerializer.SerializeToElement(WrittenName.Of(by));
        }

        return new Error(ErrorCodes.AccountStateConflict, details);
    }
}
