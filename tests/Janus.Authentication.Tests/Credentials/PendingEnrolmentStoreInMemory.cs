using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Core;

namespace Janus.Authentication.Tests.Credentials;

/// <summary>
/// The enrolments a texted code has yet to confirm, held in memory. One stands per
/// account per catalogue entry, as the key of the table holds it.
/// </summary>
internal sealed class PendingEnrolmentStoreInMemory : IPendingEnrolmentStore
{
    private readonly Dictionary<(SubjectId Subject, Factor Factor), PendingEnrolment> _pending = [];

    /// <summary>
    /// Every enrolment pending.
    /// </summary>
    public IReadOnlyCollection<PendingEnrolment> All => _pending.Values;

    /// <summary>
    /// How many times a row was taken under its lock.
    /// </summary>
    public int Locks { get; private set; }

    /// <summary>
    /// What another transaction committed on a pending enrolment while this one waited
    /// for the lock on its row, applied as the lock is taken.
    /// </summary>
    public Func<SubjectId, ValueTask>? Locking { get; set; }

    /// <inheritdoc/>
    public ValueTask<PendingEnrolment?> FindAsync(
        SubjectId subject,
        Factor factor,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_pending.GetValueOrDefault((subject, factor)));

    /// <inheritdoc/>
    public async ValueTask<PendingEnrolment?> FindForUpdateAsync(
        SubjectId subject,
        Factor factor,
        CancellationToken cancellationToken)
    {
        Locks++;

        if (Locking is not null)
        {
            await Locking(subject);
        }

        return _pending.GetValueOrDefault((subject, factor));
    }

    /// <inheritdoc/>
    public ValueTask ReplaceAsync(PendingEnrolment pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        _pending[(pending.Subject, pending.Factor)] = pending;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(PendingEnrolment pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        _pending[(pending.Subject, pending.Factor)] = pending;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(SubjectId subject, Factor factor, CancellationToken cancellationToken)
    {
        _ = _pending.Remove((subject, factor));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        (SubjectId Subject, Factor Factor)[] lapsed =
        [
            .. _pending.Where(entry => entry.Value.ExpiresAt <= now).Select(entry => entry.Key),
        ];

        foreach ((SubjectId Subject, Factor Factor) key in lapsed)
        {
            _ = _pending.Remove(key);
        }

        return ValueTask.FromResult(lapsed.Length);
    }
}
