using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Breaches;

namespace Janus.Privacy.Tests.Breaches;

/// <summary>
/// The audit trail as a privacy test needs it: the records a test holds, read back by
/// effective subject, most recent first.
/// </summary>
internal sealed class AuditTrailStoreInMemory : IAuditTrailStore
{
    private readonly List<AuditEntry> _entries = [];

    /// <summary>
    /// Holds one record.
    /// </summary>
    /// <param name="entry">The record.</param>
    public void Hold(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _entries.Add(entry);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<AuditEntry>> OfSubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<AuditEntry>>(
        [
            .. _entries
                .Where(entry => entry.Effective == subject)
                .OrderByDescending(entry => entry.OccurredAt),
        ]);
}
