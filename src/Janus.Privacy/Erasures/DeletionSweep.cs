using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Outbox;
using Janus.Privacy.Requests;

namespace Janus.Privacy.Erasures;

/// <summary>
/// One pass over the deletion windows the clock has run out on: each account is
/// erased and the fact put on the outbox, in one transaction per account.
/// </summary>
/// <param name="accounts">Where the windows that have run out are read.</param>
/// <param name="eraser">What makes the personal fields unrecoverable.</param>
/// <param name="outbox">Where the erasure is announced to the subscribers.</param>
/// <param name="audit">Where the erasure is written down.</param>
/// <param name="configuration">Where the lengths of the two windows are read.</param>
/// <param name="work">The one transaction each account is carried in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-LIFE-014, IDN-LIFE-003, IDN-ACCT-007 and PRIV-RIGHT-005. Nothing
/// here waits on a human: the window is the whole of the decision, and an account that
/// reaches its end without a cancellation or a reversal is erased. One account per
/// transaction, so a deployment that falls over mid-pass has erased whole accounts and
/// begun none.
/// </remarks>
internal sealed class DeletionSweep(
    IAccountStates accounts,
    ISubjectEraser eraser,
    IOutboxStore outbox,
    IPrivacyAudit audit,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    private static readonly AuditAction Erased = AuditActions.ErasureExecuted;

    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many accounts the pass erased.</returns>
    public async ValueTask<int> SweepAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        TimeSpan grace = await ReadAsync(Settings.AccountDeletionGrace, cancellationToken)
            .ConfigureAwait(false);
        TimeSpan takedown = await ReadAsync(Settings.TakedownGrace, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PendingDeletion> elapsed = await accounts
            .DeletingSinceAsync(now - (grace < takedown ? grace : takedown), cancellationToken)
            .ConfigureAwait(false);

        int erased = 0;

        foreach (PendingDeletion deletion in elapsed)
        {
            // IDN-LIFE-003: a takedown borrows the deletion timer and not its length,
            // so each window is measured by its own key.
            if (deletion.Since > now - (deletion.By is DeletionOrigin.Takedown ? takedown : grace))
            {
                continue;
            }

            await ErasedAsync(deletion, now, cancellationToken).ConfigureAwait(false);

            erased++;
        }

        return erased;
    }

    private async ValueTask<TimeSpan> ReadAsync(
        DurationSetting setting,
        CancellationToken cancellationToken) =>
        (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => setting.Default);

    // IDN-LIFE-003: a takedown ends in the same erasure as a request, and the
    // subscribers are told which of the two reached them.
    private static ErasureReason Because(DeletionOrigin origin) => origin switch
    {
        DeletionOrigin.Takedown => ErasureReason.MinorTakedown,
        _ => ErasureReason.ErasureRequest,
    };

    private async ValueTask ErasedAsync(
        PendingDeletion deletion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ErasureReason reason = Because(deletion.By);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        _ = await eraser.EraseAsync(deletion.Subject, reason, now, cancellationToken)
            .ConfigureAwait(false);

        // PRIV-RIGHT-005b: the fact goes on the outbox in the transaction that made
        // it true, so an erased account and the word to the host commit together.
        await outbox
            .AddAsync(
                Delivery.Of(deletion.Subject, SubjectEventKind.ErasureRequested, now, reason: reason),
                cancellationToken)
            .ConfigureAwait(false);

        await audit
            .RecordedAsync(
                Erased,
                acting: null,
                deletion.Subject,
                now,
                Named(deletion, reason),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, JsonElement> Named(
        PendingDeletion deletion,
        ErasureReason reason) =>
        new(capacity: 3, StringComparer.Ordinal)
        {
            ["reason"] = JsonSerializer.SerializeToElement(reason.ToString()),
            ["deletingBy"] = JsonSerializer.SerializeToElement(deletion.By.ToString()),
            ["deletingSince"] = JsonSerializer.SerializeToElement(deletion.Since),
        };
}
