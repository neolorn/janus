using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Breaches;

/// <summary>
/// Where the audit trail is read by subject.
/// </summary>
/// <remarks>
/// Implements PRIV-BREACH-002 and CONV-DESIGN-003. The read takes the indexes on the
/// effective and the acting subject, so it answers without a scan of the trail.
/// </remarks>
internal interface IAuditTrailStore
{
    /// <summary>
    /// The records naming one subject either way, most recent first.
    /// </summary>
    /// <param name="subject">Whose records.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records.</returns>
    ValueTask<IReadOnlyList<AuditEntry>> OfSubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken);
}
