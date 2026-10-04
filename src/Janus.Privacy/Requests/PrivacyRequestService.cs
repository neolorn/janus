using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Policies;

namespace Janus.Privacy.Requests;

/// <summary>
/// The data subject request queue.
/// </summary>
/// <param name="requests">Where the queue is.</param>
/// <param name="calendar">The deployment's working week, holidays and zone.</param>
/// <param name="scope">Whether the caller may work the queue.</param>
/// <param name="stepUp">What a fulfilment asks of the caller's session.</param>
/// <param name="accounts">Where an account enters the restricted or deleting state.</param>
/// <param name="restrictions">Where an account is restricted and the subscribers told.</param>
/// <param name="notices">Where the automatic receipt and the out-of-band deletion notice go.</param>
/// <param name="audit">Where each exercise is written down.</param>
/// <param name="configuration">Where the decision period and the warning lead are read.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, PRIV-RIGHT-001, PRIV-RIGHT-002 and chapter 09 sections 7
/// and 8a. The receipt is undertaken inside the transaction that puts the request on
/// the queue, so no receipt goes out for a request that was not queued, and a receipt a
/// sending restriction refuses leaves the request standing with no receipt.
/// </remarks>
internal sealed class PrivacyRequestService(
    IPrivacyRequestStore requests,
    WorkingCalendar calendar,
    AdministrativeScope scope,
    IStepUpGate stepUp,
    IAccountStates accounts,
    RestrictionGrant restrictions,
    ISubjectNotices notices,
    IPrivacyAudit audit,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time) : IPrivacyRequests
{
    /// <summary>
    /// What the send ledger records a request message under.
    /// </summary>
    internal const string Source = "privacy.request";

    private static readonly AuditAction Submitted = AuditActions.RequestSubmitted;

    private static readonly AuditAction Entered = AuditActions.RequestEntered;

    private static readonly AuditAction Fulfilled = AuditActions.RequestFulfilled;

    private static readonly AuditAction Refused = AuditActions.RequestRefused;

    /// <inheritdoc/>
    public async ValueTask<Result<PrivacyRequestReceipt>> SubmitAsync(
        AccessContext context,
        PrivacyRequestType type,
        string detail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Erasure is the subject's own deletion, which identifies them by their
        // session and runs the grace window; one that arrives out of band needs a
        // human to confirm who asked, so it is never submitted here.
        if (context.Effective is not SubjectId subject || type is PrivacyRequestType.Erasure)
        {
            return Result.Failure<PrivacyRequestReceipt>(Error.From(ErrorCodes.Denied));
        }

        if (Stated(detail) is not string stated)
        {
            return Result.Failure<PrivacyRequestReceipt>(Malformed("detail"));
        }

        if (await requests.OpenAsync(subject, type, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PrivacyRequestReceipt>(
                Error.From(ErrorCodes.RequestDuplicate));
        }

        DateTimeOffset now = time.GetUtcNow();
        Error? failure = null;

        DateOnly today = (await calendar.TodayAsync(now, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<DateOnly>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<PrivacyRequestReceipt>(failure);
        }

        Deadline deadline = (await ClockAsync(today, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Deadline>(error, ref failure));

        return failure is not null
            ? Result.Failure<PrivacyRequestReceipt>(failure)
            : await QueuedAsync(
                    QueuedRequest.Submitted(subject, type, stated, today, now, deadline),
                    Submitted,
                    context,
                    entered: false,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<PrivacyRequestReceipt>> EnterAsync(
        AccessContext context,
        PrivacyRequestEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entry);

        if (await scope
                .RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken)
                .ConfigureAwait(false) is Error denied)
        {
            return Result.Failure<PrivacyRequestReceipt>(denied);
        }

        // 09 section 8a: an entry's detail is optional, none where none was given, and
        // one given is held to the bound of API-CONV-002, a blank one included.
        string? detail = Stated(entry.Detail);

        if (entry.Detail is not null && detail is null)
        {
            return Result.Failure<PrivacyRequestReceipt>(Malformed("detail"));
        }

        if (Stated(entry.Channel) is not string channel)
        {
            return Result.Failure<PrivacyRequestReceipt>(Malformed("channel"));
        }

        if (Stated(entry.IdentityConfirmation) is not string confirmation)
        {
            return Result.Failure<PrivacyRequestReceipt>(Malformed("identityConfirmation"));
        }

        // 09 section 8a (D-183): a request is entered for a subject an account bears, so
        // that the fulfilment has no existence left to refuse on.
        if (await accounts.StandingAsync(entry.Subject, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<PrivacyRequestReceipt>(
                Error.From(ErrorCodes.RequestInvalid, "member", JsonSerializer.SerializeToElement("subject")));
        }

        DateTimeOffset now = time.GetUtcNow();
        Error? failure = null;

        DateOnly today = (await calendar.TodayAsync(now, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<DateOnly>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<PrivacyRequestReceipt>(failure);
        }

        // D-153: the date the request reached the company decides the deadline, so a
        // date in the future would buy the company days the statute does not give it.
        if (entry.ReceivedAt > today)
        {
            return Result.Failure<PrivacyRequestReceipt>(
                Error.From(ErrorCodes.RequestReceivedFuture));
        }

        if (await requests
                .OpenAsync(entry.Subject, entry.Type, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<PrivacyRequestReceipt>(
                Error.From(ErrorCodes.RequestDuplicate));
        }

        Deadline deadline = (await ClockAsync(entry.ReceivedAt, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<Deadline>(error, ref failure));

        return failure is not null
            ? Result.Failure<PrivacyRequestReceipt>(failure)
            : await QueuedAsync(
                    QueuedRequest.Entered(
                        entry with { Detail = detail, Channel = channel, IdentityConfirmation = confirmation },
                        now,
                        deadline),
                    Entered,
                    context,
                    entered: true,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<PrivacyRequest>>> QueueAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return await scope
                .RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken)
                .ConfigureAwait(false) is Error denied
            ? Result.Failure<IReadOnlyList<PrivacyRequest>>(denied)
            : Result.Success<IReadOnlyList<PrivacyRequest>>(
                [
                    .. (await requests.AllAsync(cancellationToken).ConfigureAwait(false))
                        .Select(request => request.Read()),
                ]);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> FulfilAsync(
        AccessContext context,
        SessionId session,
        PrivacyRequestId request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope
                .RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken)
                .ConfigureAwait(false) is Error denied)
        {
            return Result.Failure(denied);
        }

        Error? failure = null;

        QueuedRequest asked = (await DecidableAsync(request, held: false, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<QueuedRequest>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // OPS-BOOT-002, 09 section 8a: the reserved account is the one way in the
        // emergency leaves, so its erasure is refused once the account is read, before
        // the session is asked for anything.
        if (asked.Type is PrivacyRequestType.Erasure
            && (await BorneAsync(asked.Subject, cancellationToken).ConfigureAwait(false)).Emergency)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // 09 section 8a, D-166 X8: a fulfilment acts on another person's data or
        // account, so the session proves it is the caller, and it is asked last so that
        // every other refusal is told as itself.
        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if ((await stepUp
                .RequireAsync(acting, session, StepUpAction.PrivacyRequestFulfil, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await AskedAgainAsync(context, acting, asked.Subject, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: the request is decided under its row's lock, so a refusal or the
        // deadline's lapse at the same moment is decided before or after it, never both.
        QueuedRequest held = (await DecidableAsync(request, held: true, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<QueuedRequest>(error, ref failure));

        if (failure is Error undecidable)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(undecidable);
        }

        // IDN-LIFE-003: what the fulfilment could not do leaves the request open.
        bool windowStarted = (await DoneAsync(held, now, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<bool>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(failure);
        }

        held.Fulfil(now);

        await requests.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(
                Fulfilled,
                context.Acting,
                context.BreakGlassReason,
                held.Subject,
                now,
                Named(held),
                cancellationToken)
            .ConfigureAwait(false);

        // IDN-LIFE-003, AUTH-ABUSE-004: the security-notice set hears of a window an
        // out-of-band request started, undertaken in this transaction and carried after
        // its commit as the receipt is, and the notice carries no cancel
        // link; a window already running was announced when it began.
        if (windowStarted)
        {
            _ = await notices
                .TellAsync(held.Subject, MessageKind.OobDeletionNotice, Source, cancellationToken)
                .ConfigureAwait(false);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RefuseAsync(
        AccessContext context,
        PrivacyRequestId request,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope
                .RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken)
                .ConfigureAwait(false) is Error denied)
        {
            return Result.Failure(denied);
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure(Malformed("reason"));
        }

        Error? failure = null;

        _ = (await DecidableAsync(request, held: false, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<QueuedRequest>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: decided under the row's lock, as a fulfilment is.
        QueuedRequest held = (await DecidableAsync(request, held: true, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<QueuedRequest>(error, ref failure));

        if (failure is Error undecidable)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(undecidable);
        }

        held.Refuse(now, stated);

        await requests.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(
                Refused,
                context.Acting,
                context.BreakGlassReason,
                held.Subject,
                now,
                Named(held),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // API-CONV-002: a free-text field is 1 to 1024 characters after trimming.
    private static string? Stated(string? text) =>
        text?.Trim() is { Length: > 0 and <= 1024 } stated ? stated : null;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    private static Dictionary<string, JsonElement> Named(QueuedRequest request) =>
        new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
        {
            ["request"] = JsonSerializer.SerializeToElement(request.Id.ToString()),
            ["type"] = JsonSerializer.SerializeToElement(request.Type.ToString()),
            ["status"] = JsonSerializer.SerializeToElement(request.Status.ToString()),
        };

    // AUTHZ-CONCEAL-005 governs what a caller with no business here is told; a member
    // of staff working the queue under `privacyrequest:manage` has that business, so
    // what they are told apart is the permission, the identifier and the decision that
    // already stands (PRIV-RIGHT-001).
    // AUTHZ-GATE-006, D-183: the gate asked again inside the unit of work. Where the
    // request is the acting account's own, the fulfilment goes on to lock that row
    // itself, so it is taken for the change first and the gate judges it under that lock.
    private async ValueTask<Error?> AskedAgainAsync(
        AccessContext context,
        SubjectId acting,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (acting == subject)
        {
            await accounts.HoldAsync(subject, cancellationToken).ConfigureAwait(false);
        }

        return await scope
            .RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<Result<QueuedRequest>> DecidableAsync(
        PrivacyRequestId request,
        bool held,
        CancellationToken cancellationToken)
    {
        QueuedRequest? found = held
            ? await requests.FindForUpdateAsync(request, cancellationToken).ConfigureAwait(false)
            : await requests.FindAsync(request, cancellationToken).ConfigureAwait(false);

        if (found is not QueuedRequest decidable)
        {
            return Result.Failure<QueuedRequest>(Error.From(ErrorCodes.RequestNotFound));
        }

        return decidable.Open
            ? Result.Success(decidable)
            : Result.Failure<QueuedRequest>(Error.From(ErrorCodes.RequestDecided));
    }

    // What the fulfilment does to the account, answering whether it started a deletion
    // grace window.
    private async ValueTask<Result<bool>> DoneAsync(
        QueuedRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            case PrivacyRequestType.Restriction:
                _ = await restrictions
                    .ApplyAsync(request.Subject, now, cancellationToken)
                    .ConfigureAwait(false);

                return Result.Success(false);

            // 09 section 8a, IDN-LIFE-003: a fulfilled erasure follows the state it
            // finds and refuses nothing on it. An account already in its window, by any
            // origin, keeps the window running, and one already erased needs nothing;
            // any other enters the window the same way self-service deletion does,
            // because the reversal period is the subject's whichever door the request
            // came through.
            case PrivacyRequestType.Erasure:
                if (Away(await BorneAsync(request.Subject, cancellationToken).ConfigureAwait(false)))
                {
                    return Result.Success(false);
                }

                if (await accounts
                        .BeginDeletionAsync(
                            request.Subject,
                            DeletionOrigin.OutOfBandRequest,
                            now,
                            cancellationToken)
                        .ConfigureAwait(false))
                {
                    return Result.Success(true);
                }

                // D-166 X3: the window is begun under the account's lock, which this
                // transaction now holds, so one that did not begin is a window another
                // transaction began, or an erasure it finished, since the read above,
                // and the request is recorded fulfilled against it as above. A deletion
                // that will not begin from any other state is a fault (D-183).
                return Away(await BorneAsync(request.Subject, cancellationToken).ConfigureAwait(false))
                    ? Result.Success(false)
                    : throw new InvalidOperationException(
                        "The deletion a fulfilled erasure request owes did not begin.");

            // Rectification of data the subject cannot edit is the correction itself,
            // which is the deployment's own record and not the library's: what the
            // library owes is the decision and the deadline it was made inside.
            case PrivacyRequestType.Rectification:
            default:
                return Result.Success(false);
        }
    }

    private static bool Away(AccountStanding standing) =>
        standing.State is AccountState.Deleting or AccountState.Deleted;

    // 09 section 8a (D-183): the entry refused a subject no account bears and an
    // account's row is never removed, so a request whose subject bears none is a broken
    // invariant, and a fault.
    private async ValueTask<AccountStanding> BorneAsync(SubjectId subject, CancellationToken cancellationToken) =>
        await accounts.StandingAsync(subject, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException("A privacy request names a subject no account bears.");

    private async ValueTask<Result<PrivacyRequestReceipt>> QueuedAsync(
        QueuedRequest request,
        AuditAction action,
        AccessContext context,
        bool entered,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<PrivacyRequestReceipt>(notBegun);
        }

        // AUTHZ-GATE-006, D-183: a request entered on a subject's behalf passed the gate,
        // which is asked again inside the unit of work, with the acting account's row
        // held before any other lock, so a restriction committed since the gate step
        // refuses the entry before anything is written. A subject's own submission asks
        // no permission and is not asked here.
        if (entered
            && await scope
                .RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken)
                .ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<PrivacyRequestReceipt>(since);
        }

        // D-166 X3: whether one of the type stands open is read again with the
        // subject's requests of it held, so two submitted at once queue one.
        await requests.HoldAsync(request.Subject, request.Type, cancellationToken).ConfigureAwait(false);

        if (await requests.OpenAsync(request.Subject, request.Type, cancellationToken).ConfigureAwait(false))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<PrivacyRequestReceipt>(Error.From(ErrorCodes.RequestDuplicate));
        }

        // PRIV-RIGHT-002 AC1, AUTH-ABUSE-004: the receipt is undertaken in the transaction
        // that queues the request and carried after its commit, because a receipt for a
        // request that was not queued would be the one thing worse than none. A receipt
        // no channel admits, as one a sending restriction refuses, does not unqueue the
        // request or stop the clock: the request is written with no receipt.
        if (await notices
                .TellAsync(request.Subject, MessageKind.PrivacyRequestReceived, Source, cancellationToken)
                .ConfigureAwait(false) > 0)
        {
            request.ReceiptAdmitted();
        }

        await requests.AddAsync(request, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(
                action,
                context.Acting,
                context.BreakGlassReason,
                request.Subject,
                now,
                Named(request),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<PrivacyRequestReceipt>(notCommitted);
        }

        return Result.Success(
            new PrivacyRequestReceipt(request.Id, request.ReceiptSentAt, request.DecisionDue));
    }

    private async ValueTask<Result<Deadline>> ClockAsync(
        DateOnly received,
        CancellationToken cancellationToken)
    {
        int days = (await configuration
                .ReadAsync(Settings.PrivacyRequestDecision, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        int lead = (await configuration
                .ReadAsync(Settings.PrivacyRequestWarningLead, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        Error? failure = null;

        DateTimeOffset due = (await calendar.DueAsync(received, days, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<DateTimeOffset>(error, ref failure));

        DateTimeOffset warnAt = (await calendar
                .WarnAtAsync(received, days, lead, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<DateTimeOffset>(error, ref failure));

        DateTimeOffset escalateAt = (await calendar.DayOfAsync(due, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<DateTimeOffset>(error, ref failure));

        return failure is not null
            ? Result.Failure<Deadline>(failure)
            : Result.Success(new Deadline(due, warnAt, escalateAt));
    }
}
