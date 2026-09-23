using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Requests;

/// <summary>
/// One pass over the requests the clock has reached: the ones to warn about, the ones
/// to escalate, and the ones whose deadline passed undecided.
/// </summary>
/// <param name="requests">Where the queue is.</param>
/// <param name="restrictions">Where an account is restricted and the subscribers told.</param>
/// <param name="notices">Where the word to the subject goes.</param>
/// <param name="alerts">Where the two deadline conditions are raised.</param>
/// <param name="audit">Where a lapse is written down.</param>
/// <param name="work">The one transaction each request is carried in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements PRIV-RIGHT-002 and PRIV-RIGHT-004. Nothing here waits on a human: with
/// one operator, a deadline that depends on a click is a deadline missed by an
/// absence. A request decided before its deadline is not reached by this pass at all,
/// which is what cancels both alerts.
/// </remarks>
internal sealed class DeadlineSweep(
    IPrivacyRequestStore requests,
    RestrictionGrant restrictions,
    ISubjectNotices notices,
    IPrivacyAlerts alerts,
    IPrivacyAudit audit,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// What the send ledger records a lapse message under.
    /// </summary>
    internal const string Source = "privacy.request.lapse";

    private static readonly AuditAction Lapsed = AuditActions.RequestLapsed;

    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many requests the pass changed.</returns>
    public async ValueTask<int> SweepAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        int carried = 0;

        foreach (QueuedRequest request in
            await requests.ReachedAsync(now, cancellationToken).ConfigureAwait(false))
        {
            if (await ReachedAsync(request, now, cancellationToken).ConfigureAwait(false))
            {
                carried++;
            }
        }

        return carried;
    }

    private static Dictionary<string, JsonElement> Named(QueuedRequest request) =>
        new(capacity: 4, StringComparer.Ordinal)
        {
            ["request"] = JsonSerializer.SerializeToElement(request.Id.ToString()),
            ["type"] = JsonSerializer.SerializeToElement(request.Type.ToString()),
            ["status"] = JsonSerializer.SerializeToElement(request.Status.ToString()),
            ["decisionDue"] = JsonSerializer.SerializeToElement(request.DecisionDue),
        };

    private async ValueTask<bool> ReachedAsync(
        QueuedRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        bool carried = false;

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (request.WarnedAt is null && now >= request.WarnAt)
        {
            request.Warned(now);
            carried = true;

            await RaisedAsync(AlertCondition.PrivacyDeadlineApproaching, request, cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.EscalatedAt is null && now >= request.EscalateAt)
        {
            request.Escalated(now);
            carried = true;

            await RaisedAsync(AlertCondition.PrivacyDeadlineReached, request, cancellationToken)
                .ConfigureAwait(false);
        }

        if (now > request.DecisionDue)
        {
            await LapsedAsync(request, now, cancellationToken).ConfigureAwait(false);

            carried = true;
        }

        if (carried)
        {
            await requests.RecordAsync(request, cancellationToken).ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return carried;
    }

    private async ValueTask LapsedAsync(
        QueuedRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // PRIV-RIGHT-002: restriction suspends action and never visibility, so the
        // lapse grants it and turns a deemed rejection into a granted request.
        if (request.Type is PrivacyRequestType.Restriction)
        {
            _ = await restrictions.ApplyAsync(request.Subject, now, cancellationToken)
                .ConfigureAwait(false);

            request.GrantByLapse(now);
        }
        else
        {
            // Erasure cannot run without a human confirming identity, so the system
            // never erases on its own: the record persists and the subject is told
            // honestly that the deadline passed.
            request.DeemRefusedByLapse(now);

            _ = await notices
                .TellAsync(request.Subject, MessageKind.PrivacyRequestLapsed, Source, cancellationToken)
                .ConfigureAwait(false);
        }

        await audit
            .RecordedAsync(
                Lapsed,
                acting: null,
                request.Subject,
                now,
                Named(request),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask RaisedAsync(
        AlertCondition condition,
        QueuedRequest request,
        CancellationToken cancellationToken) =>
        await alerts
            .RaiseAsync(
                condition,
                request.Id.ToString(),
                Named(request),
                cancellationToken)
            .ConfigureAwait(false);
}
