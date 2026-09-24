using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// What an administrator does to the standing of someone else's account: suspends it
/// and reactivates it.
/// </summary>
/// <param name="scope">Whether the caller administers accounts in the deployment.</param>
/// <param name="stepUp">What the two operations ask of the administrator's session.</param>
/// <param name="directory">Where the standing is read and the transition carried.</param>
/// <param name="sessions">What a suspension ends.</param>
/// <param name="events">Where the transition is announced.</param>
/// <param name="audit">Where what the administrator did is recorded.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-LIFE-013, AUTH-SESS-010, IDN-AUD-001 and chapter 09 section 8a. The
/// grants of a suspended account are left in place, which is what lets reactivation
/// restore them exactly; the gate confers nothing on an account that is not active.
/// </remarks>
internal sealed class AccountAdministration(
    AdministrativeScope scope,
    StepUpGuard stepUp,
    IAccountDirectory directory,
    ISessionStore sessions,
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

    private static string Key(SubjectId subject, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{subject.Value}@{at.UtcTicks}");

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
