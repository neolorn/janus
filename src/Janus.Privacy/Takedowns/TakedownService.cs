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
/// The minor takedown, of an account and of a person record.
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
/// Implements LIB-API-005, IDN-LIFE-003, IDN-LIFE-003a, PRIV-MINOR-002, AUTH-SESS-010
/// and REC-TKD-001. The trigger is the library's half in one transaction and the hosts'
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
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<ExecutedTakedown>(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await AskedAgainAsync(context, subject, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<ExecutedTakedown>(since);
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
                        Effective = context.Effective,
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

        return await ProgressAsync(subject, standing, cancellationToken).ConfigureAwait(false) is TakedownProgress progress
            ? Result.Success(progress)
            : Result.Failure<TakedownProgress>(Error.From(ErrorCodes.TakedownNotFound));
    }

    /// <summary>
    /// Takes a person record down inside the unit of work the records area opened, once
    /// it has admitted the staff member, judged the record and the step-up, and held the
    /// record: the record's half in the transaction, the hosts' as a delivery written in
    /// it, and no suspension announced, a record having no access to suspend
    /// (REC-TKD-001).
    /// </summary>
    /// <param name="context">The staff member taking it down.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="trigger">What raised the report.</param>
    /// <param name="reason">Why, trimmed and in its bounds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The takedown, or nothing where the record, read under its lock, is not taken down.</returns>
    /// <exception cref="ArgumentNullException">The context or the reason is absent.</exception>
    internal async ValueTask<ExecutedTakedown?> TakeDownRecordAsync(
        AccessContext context,
        SubjectId record,
        TakedownTrigger trigger,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reason);

        DateTimeOffset now = time.GetUtcNow();
        AccountStanding? standing = await accounts.StandingAsync(record, cancellationToken).ConfigureAwait(false);
        DeletionWindows windows = (await DeletionWindows.ReadAsync(configuration, cancellationToken)
            .ConfigureAwait(false)).ForRecords;
        var delivery = Delivery.Of(record, SubjectEventKind.TakedownExecuted, now);
        var takedown = new ExecutedTakedown(
            new TakedownId(delivery.Id.Value),
            windows.ErasureDue(
                DeletionOrigin.Takedown,
                now,
                standing is { State: AccountState.Deleting } ? standing.DeletingSince : null));

        if (!await accounts.TakeDownRecordAsync(record, now, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        await outbox.AddAsync(delivery, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(
                Executed,
                context.Acting,
                context.BreakGlassReason,
                record,
                now,
                Named(takedown, trigger, reason),
                cancellationToken)
            .ConfigureAwait(false);

        return takedown;
    }

    /// <summary>
    /// A person record's latest takedown, as an account's is read (REC-TKD-001).
    /// </summary>
    /// <param name="record">The record's subject, which the records area admitted.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The progress, or nothing where the record holds no takedown.</returns>
    internal async ValueTask<TakedownProgress?> RecordProgressAsync(
        SubjectId record,
        CancellationToken cancellationToken) =>
        await accounts.StandingAsync(record, cancellationToken).ConfigureAwait(false) is AccountStanding standing
            ? await ProgressAsync(record, standing, cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>
    /// Whether a person record's takedown may be reversed now, judged before the step-up
    /// is asked (REC-TKD-001).
    /// </summary>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where it may, or <c>identity.takedown.notfound</c>,
    /// <c>identity.takedown.windowelapsed</c>.
    /// </returns>
    internal async ValueTask<Error?> RecordReversalRefusedAsync(
        SubjectId record,
        CancellationToken cancellationToken)
    {
        if (await accounts.StandingAsync(record, cancellationToken).ConfigureAwait(false)
            is not
            {
                State: AccountState.Deleting or AccountState.Deleted,
                DeletingBy: DeletionOrigin.Takedown,
                DeletingSince: DateTimeOffset since,
            } standing)
        {
            return Error.From(ErrorCodes.TakedownNotFound);
        }

        DeletionWindows windows = (await DeletionWindows.ReadAsync(configuration, cancellationToken)
            .ConfigureAwait(false)).ForRecords;

        return standing.State is AccountState.Deleted
            || time.GetUtcNow() >= windows.ErasureDue(DeletionOrigin.Takedown, since, standing.DeletionHeldSince)
            ? Error.From(ErrorCodes.TakedownWindowElapsed)
            : null;
    }

    /// <summary>
    /// Reverses a person record's takedown inside the unit of work the records area
    /// opened, restoring what the record held at the trigger (REC-TKD-001).
    /// </summary>
    /// <param name="context">The staff member reversing it.</param>
    /// <param name="record">The record's subject.</param>
    /// <param name="reason">Why, trimmed and in its bounds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where it was reversed, or <c>identity.takedown.windowelapsed</c>,
    /// <c>identity.takedown.notfound</c> as the record stands under its lock, or the
    /// announcement's refusal.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context or the reason is absent.</exception>
    internal async ValueTask<Error?> ReverseRecordTakedownAsync(
        AccessContext context,
        SubjectId record,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reason);

        DateTimeOffset now = time.GetUtcNow();
        DeletionWindows windows = (await DeletionWindows.ReadAsync(configuration, cancellationToken)
            .ConfigureAwait(false)).ForRecords;

        // IDN-LIFE-003: the window is judged again under the lock, and an erasure that
        // committed first leaves the window closed.
        if (!await accounts.ReverseRecordTakedownAsync(record, now, windows, cancellationToken).ConfigureAwait(false))
        {
            return Error.From(
                await accounts.StandingAsync(record, cancellationToken).ConfigureAwait(false)
                    is { DeletingBy: DeletionOrigin.Takedown }
                    ? ErrorCodes.TakedownWindowElapsed
                    : ErrorCodes.TakedownNotFound);
        }

        await audit
            .RecordedAsync(Reversed, context.Acting, context.BreakGlassReason, record, now, Named(reason), cancellationToken)
            .ConfigureAwait(false);

        return (await events
                .PublishAsync(
                    new TakedownReversed(now, Key(record, now))
                    {
                        Subject = record,
                        Actor = context.Acting,
                        Effective = context.Effective,
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
    }

    // IDN-LIFE-003 AC2: the takedown stands while the subject is in the window it began,
    // or erased from it; otherwise it was reversed and no erasure is due. A person
    // record's window is measured by its own lengths (REC-REC-002).
    private async ValueTask<TakedownProgress?> ProgressAsync(
        SubjectId subject,
        AccountStanding standing,
        CancellationToken cancellationToken)
    {
        if (await outbox
                .LatestAsync(subject, SubjectEventKind.TakedownExecuted, cancellationToken)
                .ConfigureAwait(false)
            is not DeliveryProgress progress)
        {
            return null;
        }

        Delivery delivery = progress.Delivery;
        DateTimeOffset? due = null;

        if (Standing(standing, delivery.RaisedAt) is DateTimeOffset since)
        {
            DeletionWindows windows = await DeletionWindows.ReadAsync(configuration, cancellationToken)
                .ConfigureAwait(false);

            due = (standing.IsPersonRecord ? windows.ForRecords : windows)
                .ErasureDue(DeletionOrigin.Takedown, since, standing.DeletionHeldSince);
        }

        return new TakedownProgress(
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
            ]);
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
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await AskedAgainAsync(context, subject, cancellationToken).ConfigureAwait(false) is Error restricted)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(restricted);
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
                        Effective = context.Effective,
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

    // AUTHZ-GATE-006, D-183: the gate asked again inside the unit of work. Where the
    // account acted on is the acting one, the operation goes on to lock that row itself,
    // so it is taken for the change first and the gate judges it under that lock.
    private async ValueTask<Error?> AskedAgainAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (context.Acting is SubjectId acting && acting == subject)
        {
            await accounts.HoldAsync(subject, cancellationToken).ConfigureAwait(false);
        }

        return await DeniedAsync(context, cancellationToken).ConfigureAwait(false);
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
