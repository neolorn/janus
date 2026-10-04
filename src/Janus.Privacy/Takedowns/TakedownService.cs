using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;
using Janus.Privacy.Policies;
using Janus.Privacy.Requests;

namespace Janus.Privacy.Takedowns;

/// <summary>
/// The minor takedown.
/// </summary>
/// <param name="scope">Whether the caller may take an account down.</param>
/// <param name="stepUp">What the trigger and the reversal ask of the caller's session.</param>
/// <param name="accounts">Where the account is read and its transitions carried.</param>
/// <param name="outbox">Where the hosts are told, and where their confirmations are read.</param>
/// <param name="subscribers">Who the host registered to do its half.</param>
/// <param name="audit">Where the trigger and the reversal are written down.</param>
/// <param name="events">Where the suspension and the reversal are announced.</param>
/// <param name="configuration">Where the grace windows are read.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-003, IDN-LIFE-003a, PRIV-MINOR-002 and
/// AUTH-SESS-010. The trigger is the library's half in one transaction and the hosts'
/// half as a delivery written in it, so access has stopped by the time anyone is told,
/// and what the hosts have confirmed is readable from that moment. The erasure is phase
/// two and is the deletion sweep's, at the end of <c>takedown.grace</c>.
/// </remarks>
internal sealed class TakedownService(
    AdministrativeScope scope,
    IStepUpGate stepUp,
    IAccountStates accounts,
    IOutboxStore outbox,
    IEnumerable<ISubjectEventSubscriber> subscribers,
    IPrivacyAudit audit,
    IEvents events,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time) : ITakedowns
{
    private static readonly AuditAction Executed = AuditActions.TakedownExecuted;

    private static readonly AuditAction Reversed = AuditActions.TakedownReversed;

    // 10 section 5.12d: the trigger is recorded in the spelling the chapter gives it,
    // which is the name on the member.
    private static readonly JsonSerializerOptions Spelled =
        new() { Converters = { new JsonStringEnumConverter() } };

    /// <inheritdoc/>
    public async ValueTask<Result<ExecutedTakedown>> ExecuteAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        TakedownTrigger trigger,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reason);

        if (await DeniedAsync(context, cancellationToken).ConfigureAwait(false) is Error denied)
        {
            return Result.Failure<ExecutedTakedown>(denied);
        }

        if (Written(reason) is not string written)
        {
            return Result.Failure<ExecutedTakedown>(Malformed("reason"));
        }

        AccountStanding? standing = await accounts
            .StandingAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (Untaken(standing) is Error untaken)
        {
            return Result.Failure<ExecutedTakedown>(untaken);
        }

        DateTimeOffset now = time.GetUtcNow();
        DeletionWindows windows = await DeletionWindows.ReadAsync(configuration, cancellationToken)
            .ConfigureAwait(false);
        var delivery = Delivery.Of(subject, SubjectEventKind.TakedownExecuted, now);
        var takedown = new ExecutedTakedown(
            new TakedownId(delivery.Id.Value),
            windows.ErasureDue(
                DeletionOrigin.Takedown,
                now,
                standing!.State is AccountState.Deleting ? standing.DeletingSince : null));

        if (await ChallengedAsync(context, session, StepUpAction.AccountTakedown, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure<ExecutedTakedown>(challenged);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<ExecutedTakedown>(notBegun);
        }

        // AUTH-SESS-010 AC2: the suspension and the end of every session are both the
        // library's, so they are one transaction and not two steps.
        // D-166 X3, X5: refused under the account's lock, the takedown is answered for
        // the state it found there; the reserved account, which no takedown names, is
        // refused as OPS-BOOT-002 refuses it.
        if (!await accounts.TakeDownAsync(subject, now, cancellationToken).ConfigureAwait(false))
        {
            Error untakenThere =
                Untaken(await accounts.StandingAsync(subject, cancellationToken).ConfigureAwait(false))
                ?? Error.From(ErrorCodes.Denied);

            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<ExecutedTakedown>(untakenThere);
        }

        // IDN-LIFE-003a: the hosts' half is a delivery written with the transition, and
        // its confirmations are the completion record of it from this moment.
        await outbox.AddAsync(delivery, cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(
                Executed,
                context.Acting,
                context.BreakGlassReason,
                subject,
                now,
                Named(takedown, trigger, written),
                cancellationToken)
            .ConfigureAwait(false);

        // IDN-LIFE-003 and CONV-DESIGN-002: the suspension is announced at every trigger,
        // whatever state the account held, in the transaction that makes it true; no
        // deletion is, because the subject is sent nothing that would let them cancel it.
        if ((await events
                .PublishAsync(
                    new AccountSuspended(now, Key(subject, now), SuspensionOrigin.Administrator)
                    {
                        Subject = subject,
                        Actor = context.Acting,
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<ExecutedTakedown>(unpublished);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<ExecutedTakedown>(notCommitted);
        }

        return Result.Success(takedown);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<TakedownProgress>> ReadAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await DeniedAsync(context, cancellationToken).ConfigureAwait(false) is Error denied)
        {
            return Result.Failure<TakedownProgress>(denied);
        }

        if (await accounts.StandingAsync(subject, cancellationToken).ConfigureAwait(false)
            is not AccountStanding standing)
        {
            return Result.Failure<TakedownProgress>(Error.From(ErrorCodes.AccountNotFound));
        }

        if (await outbox
                .LatestAsync(subject, SubjectEventKind.TakedownExecuted, cancellationToken)
                .ConfigureAwait(false)
            is not DeliveryProgress progress)
        {
            return Result.Failure<TakedownProgress>(Error.From(ErrorCodes.TakedownNotFound));
        }

        Delivery delivery = progress.Delivery;
        DateTimeOffset? due = null;

        // IDN-LIFE-003 AC2: the takedown stands while the account is in the window it
        // began, or erased from it; otherwise it was reversed and no erasure is due.
        if (Standing(standing, delivery.RaisedAt) is DateTimeOffset since)
        {
            DeletionWindows windows = await DeletionWindows.ReadAsync(configuration, cancellationToken)
                .ConfigureAwait(false);

            due = windows.ErasureDue(DeletionOrigin.Takedown, since, standing.DeletionHeldSince);
        }

        return Result.Success(new TakedownProgress(
            new TakedownId(delivery.Id.Value),
            subject,
            delivery.RaisedAt,
            due,
            due is null,
            delivery.Status,
            delivery.Attempts,
            [
                .. subscribers.Select(subscriber => new SubscriberConfirmation(
                    subscriber.Name,
                    subscriber.Required,
                    progress.ConfirmedAt.TryGetValue(subscriber.Name, out DateTimeOffset at)
                        ? at
                        : null)),
            ]));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> ReverseAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reason);

        if (await DeniedAsync(context, cancellationToken).ConfigureAwait(false) is Error denied)
        {
            return Result.Failure(denied);
        }

        if (Written(reason) is not string written)
        {
            return Result.Failure(Malformed("reason"));
        }

        AccountStanding? standing = await accounts
            .StandingAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (standing is null)
        {
            return Result.Failure(Error.From(ErrorCodes.AccountNotFound));
        }

        if (standing is not
            {
                State: AccountState.Deleting or AccountState.Deleted,
                DeletingBy: DeletionOrigin.Takedown,
                DeletingSince: DateTimeOffset since,
            })
        {
            return Result.Failure(Error.From(ErrorCodes.TakedownNotFound));
        }

        DateTimeOffset now = time.GetUtcNow();
        DeletionWindows windows = await DeletionWindows.ReadAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        // The window is closed at its end whether or not the sweep has reached the
        // account yet: the erasure is due, and a reversal would race it.
        if (standing.State is AccountState.Deleted
            || now >= windows.ErasureDue(DeletionOrigin.Takedown, since, standing.DeletionHeldSince))
        {
            return Result.Failure(Error.From(ErrorCodes.TakedownWindowElapsed));
        }

        if (await ChallengedAsync(context, session, StepUpAction.AccountTakedownReverse, cancellationToken)
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

        // IDN-LIFE-003: the window is judged again under the lock, and an erasure that
        // committed first leaves the window closed.
        if (!await accounts.ReverseTakedownAsync(subject, now, windows, cancellationToken).ConfigureAwait(false))
        {
            ErrorCode unreversed =
                await accounts.StandingAsync(subject, cancellationToken).ConfigureAwait(false)
                    is { DeletingBy: DeletionOrigin.Takedown }
                    ? ErrorCodes.TakedownWindowElapsed
                    : ErrorCodes.TakedownNotFound;

            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(unreversed));
        }

        await audit
            .RecordedAsync(
                Reversed,
                context.Acting,
                context.BreakGlassReason,
                subject,
                now,
                Named(written),
                cancellationToken)
            .ConfigureAwait(false);

        // CONV-DESIGN-002: the reversal is announced in the transaction that makes it.
        if ((await events
                .PublishAsync(
                    new TakedownReversed(now, Key(subject, now))
                    {
                        Subject = subject,
                        Actor = context.Acting,
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error unpublished)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unpublished);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // API-CONV-002: a free-text field is 1 to 1024 characters after trimming.
    private static string? Written(string reason) =>
        reason.Trim() is { Length: > 0 and <= 1024 } written ? written : null;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    private static string Key(SubjectId subject, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{subject.Value}@{at.UtcTicks}");

    private static Dictionary<string, JsonElement> Named(
        ExecutedTakedown takedown,
        TakedownTrigger trigger,
        string reason) =>
        new(capacity: 4, StringComparer.Ordinal)
        {
            ["takedown"] = JsonSerializer.SerializeToElement(takedown.Id.ToString()),
            ["trigger"] = JsonSerializer.SerializeToElement(trigger, Spelled),
            ["reason"] = JsonSerializer.SerializeToElement(reason),
            ["erasureDue"] = JsonSerializer.SerializeToElement(takedown.ErasureDue),
        };

    private static Dictionary<string, JsonElement> Named(string reason) =>
        new(capacity: 1, StringComparer.Ordinal)
        {
            ["reason"] = JsonSerializer.SerializeToElement(reason),
        };

    // IDN-LIFE-003 AC2: the instant the takedown's window began, where the account is
    // still in it or was erased from it; nothing where it was reversed.
    private static DateTimeOffset? Standing(AccountStanding standing, DateTimeOffset triggeredAt) =>
        standing is
        {
            State: AccountState.Deleting or AccountState.Deleted,
            DeletingBy: DeletionOrigin.Takedown,
            DeletingSince: DateTimeOffset since,
        }
        && since >= triggeredAt
            ? since
            : null;

    // IDN-LIFE-003: a takedown finds an account in any state but its own and an
    // erasure; a running deletion is held and its clock kept.
    private static Error? Untaken(AccountStanding? standing) =>
        standing switch
        {
            null => Error.From(ErrorCodes.AccountNotFound),
            { State: AccountState.Deleting, DeletingBy: DeletionOrigin.Takedown } =>
                Error.From(ErrorCodes.TakedownActive),
            { State: AccountState.Deleted } => StateConflict(standing.State),
            _ => null,
        };

    private static Error StateConflict(AccountState state) =>
        Error.From(ErrorCodes.AccountStateConflict, "state", JsonSerializer.SerializeToElement(state, Spelled));

    // The gate before anything is read: a caller without the permission learns nothing
    // of the account, and a context in which no person acts is refused as one.
    private async ValueTask<Error?> DeniedAsync(AccessContext context, CancellationToken cancellationToken)
    {
        if (await scope
                .RefusedAsync(context, Permissions.TakedownExecute, cancellationToken)
                .ConfigureAwait(false) is Error denied)
        {
            return denied;
        }

        return context.Acting is SubjectId ? null : Error.From(ErrorCodes.Denied);
    }

    // 09 section 8a: the session's proof is judged after every other refusal, so a
    // request refused on what it says never asks the person to step up.
    private async ValueTask<Error?> ChallengedAsync(
        AccessContext context,
        SessionId session,
        StepUpAction action,
        CancellationToken cancellationToken) =>
        context.Acting is not SubjectId acting
            ? Error.From(ErrorCodes.Denied)
            : (await stepUp.RequireAsync(acting, session, action, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error);
}
