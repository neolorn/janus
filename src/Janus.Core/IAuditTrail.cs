using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The audit trail read by data subject, which is how "who was affected" is answered
/// inside the notification clock, and what a departing person did is reviewed.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, PRIV-BREACH-002 and chapter 09 section 8a.
/// </remarks>
public interface IAuditTrail
{
    /// <summary>
    /// Every audit record naming one subject, as the acting or the effective identity,
    /// most recent first.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="subject">Whose records.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The records, or <c>authz.denied</c> where the caller does not hold
    /// <c>audit:read</c> in the administrative organization.
    /// </returns>
    ValueTask<Result<IReadOnlyList<AuditEntry>>> OfSubjectAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken);
}
