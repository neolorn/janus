using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Privacy.Breaches;

namespace Janus.Storage.Privacy.Breaches;

/// <summary>
/// The audit trail read by subject, over the identity area's own store.
/// </summary>
/// <param name="records">Where the trail is read.</param>
/// <remarks>
/// Implements PRIV-BREACH-002 and CONV-DESIGN-003. What a record holds under the
/// subject's key does not cross into the entry.
/// </remarks>
internal sealed class AuditTrailStore(IAuditStore records) : IAuditTrailStore
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<AuditEntry>> OfSubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        [
            .. (await records.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false))
                .Select(record => new AuditEntry(
                    record.Id,
                    record.Category,
                    record.Action,
                    record.OccurredAt,
                    record.ActingSubject,
                    record.EffectiveSubject,
                    record.Organization,
                    record.Details)),
        ];
}
