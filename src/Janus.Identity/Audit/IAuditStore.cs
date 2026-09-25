using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Identity.Audit;

/// <summary>
/// Where audit records are appended and read.
/// </summary>
/// <remarks>
/// Implements IDN-AUD-001, PRIV-RET-002 and CONV-DESIGN-003. There is no method that
/// changes a record and none that removes one: the trail is append-only from the
/// application, and expired partitions are dropped outside it.
/// </remarks>
internal interface IAuditStore
{
    /// <summary>
    /// Appends one record.
    /// </summary>
    /// <param name="record">What happened.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of appending it.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// The record carries attributes to hold under a key and the subject has none.
    /// </exception>
    ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the records of one subject, most recent first.
    /// </summary>
    /// <param name="subject">Whose records to read, as the effective identity.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records.</returns>
    ValueTask<IReadOnlyList<AuditRecord>> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads every record naming one subject, as the acting or the effective identity,
    /// most recent first.
    /// </summary>
    /// <param name="subject">Whose records to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The records, each read as it reads after erasure: what it holds under a
    /// subject's key does not come back, whoever's key it is.
    /// </returns>
    ValueTask<IReadOnlyList<AuditRecord>> FindNamingAsync(
        SubjectId subject,
        CancellationToken cancellationToken);
}
