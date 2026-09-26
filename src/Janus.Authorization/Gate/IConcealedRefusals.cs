using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where the gate says it refused on a type that conceals its records.
/// </summary>
/// <remarks>
/// Implements AUTHZ-CONCEAL-001, BFF-ERR-003 and OPS-ENV-002. The gate decides which
/// refusal is concealed and the boundary decides what the caller is answered, so no
/// endpoint decides either.
/// </remarks>
internal interface IConcealedRefusals
{
    /// <summary>
    /// Records that a refusal was concealed.
    /// </summary>
    /// <param name="correlation">The audit record the refusal was written as.</param>
    void Concealed(AuditRecordId correlation);
}
