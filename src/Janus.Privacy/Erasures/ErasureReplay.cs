using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Outbox;
using Janus.Privacy.Requests;

namespace Janus.Privacy.Erasures;

/// <summary>
/// The replay of the off-host erasure ledger after a restore: every erasure it records
/// that the restored database does not, carried out again.
/// </summary>
/// <param name="accounts">Where an account's standing is read.</param>
/// <param name="erasures">Where an erasure the restore kept is found.</param>
/// <param name="eraser">What carries an erasure out again.</param>
/// <param name="outbox">Where the host is told of it again.</param>
/// <param name="audit">Where each erasure carried out again is written down.</param>
/// <param name="work">The one transaction each erasure runs in.</param>
/// <param name="time">The clock the audit is stamped with.</param>
/// <remarks>
/// Implements DR-016 AC3, DR-006a AC1 and INF-BG-002, as entry 333 of the decisions
/// pending review settles them. An erasure the restored database holds is one the
/// restore kept, since its row commits with the key's destruction (IDN-LIFE-003b AC4),
/// and it is left as it is. Any other is carried out again, at the instant and for the
/// reason the ledger records, in one transaction with its outbox record and its audit
/// row, so the host redoes its own half in the tables the restore brought back. Every
/// line is replayed, however old, and a second replay of the same ledger changes
/// nothing more.
/// </remarks>
internal sealed class ErasureReplay(
    IAccountStates accounts,
    IErasureStore erasures,
    ISubjectEraser eraser,
    IOutboxStore outbox,
    IPrivacyAudit audit,
    IUnitOfWork work,
    TimeProvider time)
{
    private static readonly AuditAction Erased = AuditActions.ErasureExecuted;

    // INF-BG-002: the replay runs as a named principal that may do this and nothing
    // else, never as the operator who started it.
    private static readonly SystemPrincipal Replaying =
        SystemPrincipal.ForDeployment("replay-erasures", "DR-016", SystemOperation.ErasureReplay);

    /// <summary>
    /// Replays the ledger.
    /// </summary>
    /// <param name="lines">Every line of the ledger, in its order.</param>
    /// <param name="cancellationToken">
    /// Abandons the replay; the erasure in hand rolls back and a second run carries on
    /// from what committed.
    /// </param>
    /// <returns>How many lines were carried out again, stood already, or named no account.</returns>
    /// <exception cref="ArgumentNullException">The lines are absent.</exception>
    public async ValueTask<ReplayedErasures> ReplayAsync(
        IReadOnlyList<ErasureLedgerLine> lines,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);

        int reapplied = 0;
        int standing = 0;
        int absent = 0;

        foreach (ErasureLedgerLine line in lines)
        {
            if (await erasures.FindBySubjectAsync(line.Subject, cancellationToken).ConfigureAwait(false)
                is not null)
            {
                standing++;
            }
            else if (await accounts.StandingAsync(line.Subject, cancellationToken).ConfigureAwait(false)
                is null)
            {
                absent++;
            }
            else
            {
                await ReappliedAsync(line, cancellationToken).ConfigureAwait(false);

                reapplied++;
            }
        }

        return new ReplayedErasures(reapplied, standing, absent);
    }

    // IDN-LIFE-003: a takedown's erasure is recorded as the takedown's, and every other
    // as a request that reached the deployment from outside the account, since the
    // ledger, not the person, is what asks for it now.
    private static DeletionOrigin Origin(ErasureReason reason) => reason switch
    {
        ErasureReason.MinorTakedown => DeletionOrigin.Takedown,
        _ => DeletionOrigin.OutOfBandRequest,
    };

    private static Dictionary<string, JsonElement> Named(ErasureLedgerLine line) =>
        new(capacity: 2, StringComparer.Ordinal)
        {
            ["reason"] = JsonSerializer.SerializeToElement(line.Reason.ToString()),
            ["erasedAt"] = JsonSerializer.SerializeToElement(line.ErasedAt),
        };

    private async ValueTask ReappliedAsync(ErasureLedgerLine line, CancellationToken cancellationToken)
    {
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        _ = await eraser
            .ReapplyAsync(line.Subject, line.Reason, Origin(line.Reason), line.ErasedAt, cancellationToken)
            .ConfigureAwait(false);

        // PRIV-RIGHT-005b: the restore brought the host's own rows back too, so the fact
        // goes on the outbox again, in the transaction that made it true again.
        await outbox
            .AddAsync(
                Delivery.Of(line.Subject, SubjectEventKind.ErasureRequested, line.ErasedAt, reason: line.Reason),
                cancellationToken)
            .ConfigureAwait(false);

        await audit
            .RecordedAsync(
                Erased,
                Replaying,
                line.Subject,
                time.GetUtcNow(),
                Named(line),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
