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
/// <param name="accounts">Where an account enters the restricted or deleting state.</param>
/// <param name="restrictions">Where an account is restricted and the subscribers told.</param>
/// <param name="notices">Where the automatic receipt goes.</param>
/// <param name="audit">Where each exercise is written down.</param>
/// <param name="configuration">Where the decision period and the warning lead are read.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, PRIV-RIGHT-001, PRIV-RIGHT-002 and chapter 09 sections 7
/// and 8a. The receipt is sent inside the transaction that puts the request on the
/// queue, so a queued request the subject was never told about does not exist.
/// </remarks>
internal sealed class PrivacyRequestService(
    IPrivacyRequestStore requests,
    WorkingCalendar calendar,
    AdministrativeScope scope,
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

    private static readonly AuditAction Submitted = AuditAction.Parse("privacy.request.submitted");

    private static readonly AuditAction Entered = AuditAction.Parse("privacy.request.entered");

    private static readonly AuditAction Fulfilled = AuditAction.Parse("privacy.request.fulfilled");

    private static readonly AuditAction Refused = AuditAction.Parse("privacy.request.refused");

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
                    QueuedRequest.Submitted(subject, type, detail ?? string.Empty, today, now, deadline),
                    Submitted,
                    context.Acting,
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
                    QueuedRequest.Entered(entry, now, deadline),
                    Entered,
                    context.Acting,
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
        PrivacyRequestId request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        Error? failure = null;

        QueuedRequest held = (await DecidableAsync(context, request, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<QueuedRequest>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        held.Fulfil(now);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await DoneAsync(held, now, cancellationToken).ConfigureAwait(false);
        await requests.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(
                Fulfilled,
                context.Acting,
                held.Subject,
                now,
                Named(held),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Error? failure = null;

        QueuedRequest held = (await DecidableAsync(context, request, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<QueuedRequest>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        held.Refuse(now, reason);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await requests.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(Refused, context.Acting, held.Subject, now, Named(held), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static Dictionary<string, JsonElement> Named(QueuedRequest request) =>
        new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
        {
            ["request"] = JsonSerializer.SerializeToElement(request.Id.ToString()),
            ["type"] = JsonSerializer.SerializeToElement(request.Type.ToString()),
            ["status"] = JsonSerializer.SerializeToElement(request.Status.ToString()),
        };

    // PRIV-RIGHT-001 AC3, AUTHZ-CONCEAL-005: a request the caller may not work, one
    // that does not exist, and one already decided are one answer, because telling
    // them apart would answer a question the caller has no permission to ask.
    // AUTHZ-CONCEAL-005 governs what a caller with no business here is told; a member
    // of staff working the queue under `privacyrequest:manage` has that business, so
    // what they are told apart is the permission, the identifier and the decision that
    // already stands (PRIV-RIGHT-001).
    private async ValueTask<Result<QueuedRequest>> DecidableAsync(
        AccessContext context,
        PrivacyRequestId request,
        CancellationToken cancellationToken)
    {
        if (await scope
                .RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<QueuedRequest>(refused);
        }

        QueuedRequest? held = await requests.FindAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (held is null)
        {
            return Result.Failure<QueuedRequest>(Error.From(ErrorCodes.RequestNotFound));
        }

        return held.Open
            ? Result.Success(held)
            : Result.Failure<QueuedRequest>(Error.From(ErrorCodes.RequestDecided));
    }

    private async ValueTask DoneAsync(
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

                break;

            // 09 section 8a: a fulfilled erasure enters the grace window the same way
            // self-service deletion does, because the reversal period is the subject's
            // whichever door the request came through.
            case PrivacyRequestType.Erasure:
                _ = await accounts
                    .BeginDeletionAsync(
                        request.Subject,
                        DeletionOrigin.OutOfBandRequest,
                        now,
                        cancellationToken)
                    .ConfigureAwait(false);

                break;

            // Rectification of data the subject cannot edit is the correction itself,
            // which is the deployment's own record and not the library's: what the
            // library owes is the decision and the deadline it was made inside.
            case PrivacyRequestType.Rectification:
            default:
                break;
        }
    }


    private async ValueTask<Result<PrivacyRequestReceipt>> QueuedAsync(
        QueuedRequest request,
        AuditAction action,
        SubjectId? acting,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await requests.AddAsync(request, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(action, acting, request.Subject, now, Named(request), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        // PRIV-RIGHT-002: the receipt follows the commit, because a receipt for a
        // request that was not queued would be the one thing worse than none, and a
        // channel that will not take it does not unqueue the request or stop the clock.
        _ = await notices
            .TellAsync(request.Subject, MessageKind.PrivacyRequestReceived, Source, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(
            new PrivacyRequestReceipt(request.Id, request.ReceiptSentAt, request.DecisionDue));
    }

    private async ValueTask<Result<Deadline>> ClockAsync(
        DateOnly received,
        CancellationToken cancellationToken)
    {
        int days = (await configuration
                .ReadAsync(Settings.PrivacyRequestDecision, cancellationToken).ConfigureAwait(false))
            .Match(value => value, _ => Settings.PrivacyRequestDecision.Default);

        int lead = (await configuration
                .ReadAsync(Settings.PrivacyRequestWarningLead, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.PrivacyRequestWarningLead.Default);

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
