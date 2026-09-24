using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Outbox;
using Janus.Privacy.Policies;

namespace Janus.Privacy.Erasures;

/// <summary>
/// The erasures an operator reads and, where a subscriber could not finish, completes.
/// </summary>
/// <param name="scope">Whether the caller may work the erasures.</param>
/// <param name="stepUp">What the manual completion asks of the caller's session.</param>
/// <param name="outbox">Where each erasure's delivery and its confirmations are.</param>
/// <param name="erasures">Where the erasure's own row is carried in step with its delivery.</param>
/// <param name="subscribers">Who the host registered to do its half.</param>
/// <param name="ledger">Where an erasure is written down off the host, if the deployment registered one.</param>
/// <param name="audit">Where a manual completion is written down.</param>
/// <param name="work">The one transaction a completion runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-003a, IDN-LIFE-003b and DR-016. An erasure is read
/// from the delivery its host-side work travels on, which carries its subject, reason,
/// status, attempts and confirmations, and which the erasures row follows step for
/// step. The manual path is for permanent failure and is itself recorded, so an erasure
/// never closes without a trace of who closed it or what was outstanding. The operator
/// vouches for the host's subscribers and never for the ledger line: the path appends
/// it where it is outstanding, and closes nothing until it is durable (DR-016 AC2).
/// </remarks>
internal sealed class ErasureService(
    AdministrativeScope scope,
    IStepUpGate stepUp,
    IOutboxStore outbox,
    IErasureStore erasures,
    IEnumerable<ISubjectEventSubscriber> subscribers,
    IErasureLedger? ledger,
    IPrivacyAudit audit,
    IUnitOfWork work,
    TimeProvider time) : IErasures
{
    private static readonly AuditAction Completed = AuditActions.ErasureCompleted;

    private readonly IReadOnlyList<ISubjectEventSubscriber> _waitedFor =
        ErasureLedgerSubscriber.Joined(subscribers, ledger);

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<ErasureProgress>>> ListAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope
                .RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken)
                .ConfigureAwait(false) is Error denied)
        {
            return Result.Failure<IReadOnlyList<ErasureProgress>>(denied);
        }

        IReadOnlyList<DeliveryProgress> outstanding = await outbox
            .OutstandingAsync(SubjectEventKind.ErasureRequested, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<ErasureProgress>>([.. outstanding.Select(Progress)]);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<ErasureProgress>> ReadAsync(
        AccessContext context,
        ErasureId erasure,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope
                .RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken)
                .ConfigureAwait(false) is Error denied)
        {
            return Result.Failure<ErasureProgress>(denied);
        }

        return await outbox
                .ProgressAsync(new DeliveryId(erasure.Value), cancellationToken)
                .ConfigureAwait(false)
            is { Delivery.Kind: SubjectEventKind.ErasureRequested } progress
            ? Result.Success(Progress(progress))
            : Result.Failure<ErasureProgress>(Error.From(ErrorCodes.ErasureNotFound));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> CompleteAsync(
        AccessContext context,
        SessionId session,
        ErasureId erasure,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await RefusedAsync(context, session, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (await outbox.FindAsync(new DeliveryId(erasure.Value), cancellationToken).ConfigureAwait(false)
            is not { Kind: SubjectEventKind.ErasureRequested } delivery)
        {
            return Result.Failure(Error.From(ErrorCodes.ErasureNotFound));
        }

        if (delivery.Status is not ErasureStatus.Failed)
        {
            return Result.Failure(Error.From(ErrorCodes.ErasureNotFailed));
        }

        if (await LedgeredAsync(delivery, cancellationToken).ConfigureAwait(false) is Error unwritten)
        {
            return Result.Failure(unwritten);
        }

        string[] outstanding = Outstanding(delivery);

        delivery.CompleteManually();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await outbox.RecordAsync(delivery, cancellationToken).ConfigureAwait(false);
        await ClosedAsync(delivery.Subject, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(
                Completed,
                context.Acting,
                delivery.Subject,
                time.GetUtcNow(),
                Named(erasure, outstanding),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static Dictionary<string, JsonElement> Named(ErasureId erasure, string[] outstanding) =>
        new(capacity: 2, StringComparer.Ordinal)
        {
            ["erasure"] = JsonSerializer.SerializeToElement(erasure.ToString()),
            ["outstanding"] = JsonSerializer.SerializeToElement(outstanding),
        };

    private ErasureProgress Progress(DeliveryProgress progress)
    {
        Delivery delivery = progress.Delivery;

        return new ErasureProgress(
            new ErasureId(delivery.Id.Value),
            delivery.Subject,
            delivery.Reason,
            delivery.Status,
            delivery.Attempts,
            [
                .. _waitedFor.Select(subscriber => new SubscriberConfirmation(
                    subscriber.Name,
                    subscriber.Required,
                    progress.ConfirmedAt.TryGetValue(subscriber.Name, out DateTimeOffset at)
                        ? at
                        : null)),
            ]);
    }

    // What the operator vouches for: the required subscribers that had not confirmed
    // when the budget was spent.
    private string[] Outstanding(Delivery delivery) =>
    [
        .. subscribers
            .Where(subscriber => subscriber.Required && !delivery.Confirmed.Contains(subscriber.Name))
            .Select(subscriber => subscriber.Name),
    ];

    // DR-016 AC2: the line is appended here where the delivery's attempts never made it
    // durable, and nothing is closed while it is not. A line appended here and then not
    // recorded is appended again by the next attempt, which a replay reads as one.
    private async ValueTask<Error?> LedgeredAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        if (ledger is null || delivery.Confirmed.Contains(ErasureLedgerSubscriber.Called))
        {
            return null;
        }

        // The ledger is the environment's, so what it answered is a fault here and not
        // a refusal whose code the caller would read.
        if (!(await new ErasureLedgerSubscriber(ledger)
                .HandleAsync(delivery.Raised(), cancellationToken)
                .ConfigureAwait(false))
            .Match(() => true, _ => false))
        {
            return Error.From(
                ErrorCodes.SystemFault,
                "handler",
                JsonSerializer.SerializeToElement(ErasureLedgerSubscriber.Called));
        }

        delivery.Confirm(ErasureLedgerSubscriber.Called);

        return null;
    }

    // The erasure's own row followed the delivery into failure, so it follows it out
    // rather than describing work that is done.
    private async ValueTask ClosedAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        if (await erasures.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            is not { Status: ErasureStatus.Failed } row)
        {
            return;
        }

        row.CompleteManually();

        await erasures.RecordAsync(row, cancellationToken).ConfigureAwait(false);
    }

    // The gate before anything is read, then the session's proof: a caller without the
    // permission learns nothing of the erasure, and one with it proves it is them.
    private async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken)
    {
        if (await scope
                .RefusedAsync(context, Permissions.PrivacyRequestManage, cancellationToken)
                .ConfigureAwait(false) is Error denied)
        {
            return denied;
        }

        return context.Acting is not SubjectId acting
            ? Error.From(ErrorCodes.Denied)
            : (await stepUp
                    .RequireAsync(acting, session, StepUpAction.ErasureComplete, cancellationToken)
                    .ConfigureAwait(false))
                .Match(() => (Error?)null, error => error);
    }
}
