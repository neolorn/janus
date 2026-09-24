using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// One set of recovery codes per account, held in memory as the table holds it.
/// </summary>
internal sealed class RecoveryCodeStoreInMemory : IRecoveryCodeStore
{
    private readonly Dictionary<SubjectId, RecoveryCodeSet> _held = [];

    private readonly HashSet<SubjectId> _inactive = [];

    /// <summary>
    /// Marks an account as no longer active, as the accounts table would hold it.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    public void Deactivate(SubjectId subject) => _inactive.Add(subject);

    /// <inheritdoc/>
    public ValueTask<RecoveryCodeSet?> FindAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_held.GetValueOrDefault(subject));

    /// <inheritdoc/>
    public ValueTask ReplaceAsync(RecoveryCodeSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);

        _held[set.Subject] = set;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(RecoveryCodeSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);

        _held[set.Subject] = set;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        _ = _held.Remove(subject);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SubjectId>> DueReminderAsync(
        DateTimeOffset generatedBy,
        int count,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<SubjectId>>(
        [
            .. _held.Values
                .Where(set => set.RemindedAt is null
                    && set.GeneratedAt <= generatedBy
                    && !_inactive.Contains(set.Subject))
                .OrderBy(set => set.GeneratedAt)
                .Take(count)
                .Select(set => set.Subject),
        ]);
}
