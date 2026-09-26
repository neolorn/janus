using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// An account's recovery codes, over the <c>recovery_code_sets</c> and
/// <c>recovery_codes</c> tables.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements AUTH-FACT-008, AUTH-FACT-009 and CONV-DESIGN-003. Replacing a set
/// removes the codes of the one before it, so no code of a previous set validates.
/// </remarks>
internal sealed class RecoveryCodeStore(StoreContext context) : IRecoveryCodeStore
{
    /// <inheritdoc/>
    public async ValueTask<RecoveryCodeSet?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        RecoveryCodeSetRecord? record = await context.RecoveryCodeSets
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        List<RecoveryCodeRecord> codes = await CodesAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        return RecoveryCodeSet.Existing(
            record.Subject,
            [.. codes.Select(code => new RecoveryCodeEntry(PasswordHash.Parse(code.Hash), code.UsedAt))],
            record.GeneratedAt,
            record.ViewedAt,
            record.ExportedAt,
            record.RemindedAt);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        RecoveryCodeSetRecord? record = await context.RecoveryCodeSets
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return;
        }

        context.RecoveryCodes.RemoveRange(
            await CodesAsync(subject, cancellationToken).ConfigureAwait(false));
        context.RecoveryCodeSets.Remove(record);
    }

    /// <inheritdoc/>
    public async ValueTask ReplaceAsync(RecoveryCodeSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);

        RecoveryCodeSetRecord? record = await context.RecoveryCodeSets
            .FindAsync([set.Subject], cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            record = new RecoveryCodeSetRecord { Subject = set.Subject };
            context.RecoveryCodeSets.Add(record);
        }
        else
        {
            context.RecoveryCodes.RemoveRange(
                await CodesAsync(set.Subject, cancellationToken).ConfigureAwait(false));
        }

        record.GeneratedAt = set.GeneratedAt;
        record.ViewedAt = set.ViewedAt;
        record.ExportedAt = set.ExportedAt;
        record.RemindedAt = set.RemindedAt;

        for (int ordinal = 0; ordinal < set.Codes.Count; ordinal++)
        {
            context.RecoveryCodes.Add(new RecoveryCodeRecord
            {
                Subject = set.Subject,
                Ordinal = ordinal,
                Hash = set.Codes[ordinal].Hash.Encoded,
                UsedAt = set.Codes[ordinal].UsedAt,
            });
        }
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(RecoveryCodeSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);

        RecoveryCodeSetRecord record = await context.RecoveryCodeSets
            .FindAsync([set.Subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The account has no set to carry the change.");

        record.ViewedAt = set.ViewedAt;
        record.ExportedAt = set.ExportedAt;
        record.RemindedAt = set.RemindedAt;

        List<RecoveryCodeRecord> codes = await CodesAsync(set.Subject, cancellationToken)
            .ConfigureAwait(false);

        foreach (RecoveryCodeRecord code in codes)
        {
            code.UsedAt = set.Codes[code.Ordinal].UsedAt;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SubjectId>> DueReminderAsync(
        DateTimeOffset generatedBy,
        int count,
        CancellationToken cancellationToken) =>
        await context.RecoveryCodeSets
            .Where(set => set.RemindedAt == null
                && set.GeneratedAt <= generatedBy
                && context.Accounts.Any(account =>
                    account.Subject == set.Subject && account.State == AccountState.Active))
            .OrderBy(set => set.GeneratedAt)
            .ThenBy(set => set.Subject)
            .Take(count)
            .Select(set => set.Subject)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<List<RecoveryCodeRecord>> CodesAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.RecoveryCodes
            .Where(code => code.Subject == subject)
            .OrderBy(code => code.Ordinal)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
