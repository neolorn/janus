using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where a refusal is recorded and where a correlation identifier is read back.
/// </summary>
/// <remarks>
/// Implements AUTHZ-CONCEAL-004, AUTHZ-GATE-004 and CONV-DESIGN-003. The trail is the
/// same one identity writes to; this port is the narrow view of it the gate needs,
/// because an area project reaches no other area project (CONV-LAYOUT-001).
/// </remarks>
internal interface IAccessAudit
{
    /// <summary>
    /// Records one refusal.
    /// </summary>
    /// <param name="denial">What was refused, and to whom.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(DeniedAccess denial, CancellationToken cancellationToken);

    /// <summary>
    /// Reads back the refusal a correlation identifier stands for.
    /// </summary>
    /// <param name="correlation">The identifier the refusal was answered with.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The refusal, or nothing where the identifier stands for none.</returns>
    ValueTask<DeniedAccess?> FindAsync(
        AuditRecordId correlation,
        CancellationToken cancellationToken);
}
