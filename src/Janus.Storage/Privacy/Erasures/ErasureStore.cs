using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Erasures;

/// <summary>
/// Erasures, over the <c>erasures</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements IDN-LIFE-003b and CONV-DESIGN-003. The row is created by the erasure
/// itself, so this store reads it and carries the host-side progress onto it.
/// </remarks>
internal sealed class ErasureStore(JanusDbContext context) : IErasureStore
{
    /// <inheritdoc/>
    public async ValueTask<Erasure?> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ErasureRecord? record = await context.Erasures
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Erasure>> FindIncompleteAsync(
        CancellationToken cancellationToken)
    {
        List<ErasureRecord> records = await context.Erasures
            .Where(erasure => erasure.Status != ErasureStatus.Complete)
            .OrderBy(erasure => erasure.RequestedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var erasures = new List<Erasure>(records.Count);

        foreach (ErasureRecord record in records)
        {
            erasures.Add(Read(record));
        }

        return erasures;
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Erasure erasure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(erasure);

        ErasureRecord record = await context.Erasures
            .FindAsync([erasure.Subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no erasure row to carry the progress.");

        record.Status = erasure.Status;
        record.Attempts = erasure.Attempts;
    }

    private static Erasure Read(ErasureRecord record) =>
        Erasure.Existing(
            record.Subject,
            record.RequestedAt,
            record.Reason,
            record.Status,
            record.Attempts);
}
