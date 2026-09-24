using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Accounts;

/// <summary>
/// What an administrator does to the standing of someone else's account: suspends it,
/// reactivates it, lifts its restriction and cancels its deletion.
/// </summary>
/// <param name="scope">Whether the caller administers accounts in the deployment.</param>
/// <param name="stepUp">What the gated operations ask of the administrator's session.</param>
/// <param name="directory">Where the standing is read and the transition carried.</param>
/// <param name="sessions">What a suspension ends.</param>
/// <param name="links">Where the link a deletion notice carried is held.</param>
/// <param name="configuration">Where the grace window is read.</param>
/// <param name="events">Where the transition is announced.</param>
/// <param name="audit">Where what the administrator did is recorded.</param>
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
    ISessionStore sessions,
    ILifecycleLinkStore links,
    IConfigurationStore configuration,
    IEvents events,
    IAccountAudit audit,
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

        AccountState? state = await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false);

        switch (state)
        {
            case null:
                return Result.Failure(Malformed("subject"));
            case AccountState.Deleting or AccountState.Deleted:
                return Result.Failure(Error.From(ErrorCodes.Denied));
            case AccountState.Suspended
                when await directory.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)
                    is SuspensionOrigin.Administrator:
                return Result.Success();
            default:
                break;
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.AccountSuspend, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
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
            .AdministeredAsync(AuditActions.AccountSuspended, acting, subject, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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

        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(Malformed("subject"));
        }

        // IDN-LIFE-013: the two suspensions are reversed differently, and what its owner
        // deactivated is theirs to stand back up.
        if (await directory.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)
            is not SuspensionOrigin.Administrator)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.AccountReactivate, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
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
            .AdministeredAsync(AuditActions.AccountReactivated, acting, subject, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> LiftRestrictionAsync(
        AccessContext context,
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

        switch (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false))
        {
            case null:
                return Result.Failure(Malformed("subject"));

            // PRIV-RIGHT-004: a restriction held while the account is suspended or
            // deleting stays until the account is back in the restricted state.
            case not AccountState.Restricted:
                return Result.Failure(Error.From(ErrorCodes.Denied));
            default:
                break;
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await directory.LiftRestrictionAsync(subject, now, cancellationToken).ConfigureAwait(false);
        await audit
            .AdministeredAsync(AuditActions.RestrictionLifted, acting, subject, now, cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> CancelDeletionAsync(
        AccessContext context,
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

        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure(Malformed("subject"));
        }

        if (await directory.DeletingAsync(subject, cancellationToken).ConfigureAwait(false)
            is not HeldDeletion deleting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // IDN-LIFE-003: a takedown is reversed through its own operation, never cancelled.
        if (deleting.By is DeletionOrigin.Takedown)
        {
            return Result.Failure(Error.From(ErrorCodes.TakedownActive));
        }

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.AccountDeletionGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => TimeSpan.Zero);
        DateTimeOffset now = time.GetUtcNow();

        if (now >= deleting.Since + grace)
        {
            return Result.Failure(Error.From(ErrorCodes.DeletionWindowElapsed));
        }

        // IDN-LIFE-003: the cancellation of a window an out-of-band request began is
        // recorded against that request.
        PrivacyRequestId? request = deleting.By is DeletionOrigin.OutOfBandRequest
            ? await directory.ErasureRequestAsync(subject, deleting.Since, cancellationToken).ConfigureAwait(false)
            : null;

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
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
            .CancelledOnBehalfAsync(acting, subject, request, now, cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static string Key(SubjectId subject, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{subject.Value}@{at.UtcTicks}");

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
