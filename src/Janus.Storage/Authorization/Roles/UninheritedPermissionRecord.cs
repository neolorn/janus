using Janus.Core;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// The <c>uninherited_permissions</c> row: one of the host's actions declared not
/// inherited.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-006 (D-278). The table is the host's declaration as the last
/// start wrote it, so the view a grant is read through, which the host's own queries
/// read too, passes no duty through an inclusion.
/// </remarks>
internal sealed class UninheritedPermissionRecord
{
    /// <summary>
    /// The <c>permission</c> column, which is this table's key.
    /// </summary>
    public Permission Permission { get; set; }
}
