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
/// <param name="configuration">Where the grace window is read.</param>
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

        if (await RefusedAsync(context, session, StepUpAction.AccountTakedown, cancellationToken)
                .ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<ExecutedTakedown>(refused);
        }

        if (Written(reason) is not string written)
        {
            return Result.Failure<ExecutedTakedown>(Malformed("reason"));
        }

        AccountStanding? standing = await accounts
            .StandingAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (standing is { State: AccountState.Deleting, DeletingBy: DeletionOrigin.Takedown })
        {
            return Result.Failure<ExecutedTakedown>(Error.From(ErrorCodes.TakedownActive));
        }

        // An account already in a deletion window of another origin, or already
        // erased, has nothing left that a takedown stops.
        if (standing is not { State: AccountState.Active or AccountState.Restricted or AccountState.Suspended })
        {
            return Result.Failure<ExecutedTakedown>(Error.From(ErrorCodes.Denied));
        }

        DateTimeOffset now = time.GetUtcNow();
        TimeSpan grace = await GraceAsync(cancellationToken).ConfigureAwait(false);
        var delivery = Delivery.Of(subject, SubjectEventKind.TakedownExecuted, now);
        var takedown = new ExecutedTakedown(new TakedownId(delivery.Id.Value), now + grace);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        // AUTH-SESS-010 AC2: the suspension and the end of every session are both the
        // library's, so they are one transaction and not two steps.
        if (!await accounts.TakeDownAsync(subject, now, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ExecutedTakedown>(Error.From(ErrorCodes.Denied));
        }

        // IDN-LIFE-003a: the hosts' half is a delivery written with the transition, and
        // its confirmations are the completion record of it from this moment.
        await outbox.AddAsync(delivery, cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(
                Executed,
                context.Acting,
                subject,
                now,
                Named(takedown, trigger, written),
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        // IDN-LIFE-003: the suspension is announced at the trigger, and no deletion is,
        // because the subject is sent nothing that would let them cancel it.
        Result published = await events
            .PublishAsync(
                new AccountSuspended(now, Key(subject, now), SuspensionOrigin.Administrator)
                {
                    Subject = subject,
                    Actor = context.Acting,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return published.Match(
            () => Result.Success(takedown),
            Result.Failure<ExecutedTakedown>);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<TakedownProgress>> ReadAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope
                .RefusedAsync(context, Permissions.TakedownExecute, cancellationToken)
                .ConfigureAwait(false) is Error denied)
        {
            return Result.Failure<TakedownProgress>(denied);
        }

        if (await outbox
                .LatestAsync(subject, SubjectEventKind.TakedownExecuted, cancellationToken)
                .ConfigureAwait(false)
            is not DeliveryProgress progress)
        {
            return Result.Failure<TakedownProgress>(Error.From(ErrorCodes.TakedownNotFound));
        }

        TimeSpan grace = await GraceAsync(cancellationToken).ConfigureAwait(false);
        Delivery delivery = progress.Delivery;

        return Result.Success(new TakedownProgress(
            new TakedownId(delivery.Id.Value),
            subject,
            delivery.RaisedAt,
            delivery.RaisedAt + grace,
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

        if (await RefusedAsync(context, session, StepUpAction.AccountTakedownReverse, cancellationToken)
                .ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (Written(reason) is not string written)
        {
            return Result.Failure(Malformed("reason"));
        }

        AccountStanding? standing = await accounts
            .StandingAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (standing is not { DeletingBy: DeletionOrigin.Takedown })
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        DateTimeOffset now = time.GetUtcNow();
        TimeSpan grace = await GraceAsync(cancellationToken).ConfigureAwait(false);

        // The window is closed at its end whether or not the sweep has reached the
        // account yet: the erasure is due, and a reversal would race it.
        if (standing.State is AccountState.Deleted
            || standing.DeletingSince is not DateTimeOffset since
            || now >= since + grace)
        {
            return Result.Failure(Error.From(ErrorCodes.TakedownWindowElapsed));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (!await accounts.ReverseTakedownAsync(subject, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        await audit
            .RecordedAsync(Reversed, context.Acting, subject, now, Named(written), cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return await events
            .PublishAsync(
                new TakedownReversed(now, Key(subject, now))
                {
                    Subject = subject,
                    Actor = context.Acting,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? Written(string reason) =>
        reason.Trim() is { Length: > 0 } written ? written : null;

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

    // The gate before anything is read, then the session's proof: a caller without the
    // permission learns nothing of the account, and one with it proves it is them.
    private async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SessionId session,
        StepUpAction action,
        CancellationToken cancellationToken)
    {
        if (await scope
                .RefusedAsync(context, Permissions.TakedownExecute, cancellationToken)
                .ConfigureAwait(false) is Error denied)
        {
            return denied;
        }

        return context.Acting is not SubjectId acting
            ? Error.From(ErrorCodes.Denied)
            : (await stepUp.RequireAsync(acting, session, action, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error);
    }

    private async ValueTask<TimeSpan> GraceAsync(CancellationToken cancellationToken) =>
        (await configuration.ReadAsync(Settings.TakedownGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => Settings.TakedownGrace.Default);
}
