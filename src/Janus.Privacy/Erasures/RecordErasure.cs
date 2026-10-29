using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Outbox;

namespace Janus.Privacy.Erasures;

/// <summary>
/// A person record's erasure at a host's word, at once and outside any window, once the
/// records area has admitted the caller and judged the record.
/// </summary>
/// <param name="eraser">What makes the personal fields unrecoverable.</param>
/// <param name="outbox">Where the erasure is announced to the subscribers.</param>
/// <param name="audit">Where the erasure is written down.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements REC-ADM-003, REC-REC-003, PRIV-RIGHT-005b and D-223. It runs inside the
/// unit of work the records area opened, the record held under its lock, so the erasure,
/// its row, its announcement and its entry commit together or not at all.
/// </remarks>
internal sealed class RecordErasure(
    ISubjectEraser eraser,
    IOutboxStore outbox,
    IPrivacyAudit audit,
    TimeProvider time)
{
    private static readonly AuditAction Erased = AuditActions.ErasureExecuted;

    /// <summary>
    /// Erases the record with the reason <c>retention</c>, announces it, and records it
    /// with the principal that asked and the reason it gave.
    /// </summary>
    /// <param name="context">The staff member or the job erasing it.</param>
    /// <param name="record">The record's subject, which the records area admitted.</param>
    /// <param name="reason">Why, trimmed and in its bounds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of erasing it.</returns>
    /// <exception cref="ArgumentNullException">The context or the reason is absent.</exception>
    internal async ValueTask EraseAsync(
        AccessContext context,
        SubjectId record,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reason);

        DateTimeOffset now = time.GetUtcNow();

        _ = await eraser.EraseAsync(record, ErasureReason.Retention, now, cancellationToken).ConfigureAwait(false);

        // PRIV-RIGHT-005b: the fact goes on the outbox in the transaction that made it
        // true, as every erasure's does.
        await outbox
            .AddAsync(
                Delivery.Of(record, SubjectEventKind.ErasureRequested, now, reason: ErasureReason.Retention),
                cancellationToken)
            .ConfigureAwait(false);

        // D-223: details.reason keeps its section 5.12a spelling, which the replay of the
        // off-host ledger reads, and the reason given is details.statedReason.
        Dictionary<string, JsonElement> details = new(capacity: 2, StringComparer.Ordinal)
        {
            ["reason"] = JsonSerializer.SerializeToElement(ErasureLedgerLine.Spelling(ErasureReason.Retention)),
            ["statedReason"] = JsonSerializer.SerializeToElement(reason),
        };

        if (context.Principal is SystemPrincipal principal)
        {
            await audit
                .RecordedAsync(Erased, principal, record, organization: null, now, details, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await audit
            .RecordedAsync(Erased, context.Acting, context.BreakGlassReason, record, now, details, cancellationToken)
            .ConfigureAwait(false);
    }
}
