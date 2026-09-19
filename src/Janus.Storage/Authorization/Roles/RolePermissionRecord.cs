using Janus.Core;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// The <c>role_permissions</c> row.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004 and CONV-DESIGN-003. What a role allows lives here and
/// nowhere else, so a grant naming the role confers whatever these rows say at the
/// instant the question is asked (AUTHZ-CACHE-001).
/// </remarks>
internal sealed class RolePermissionRecord
{
    /// <summary>
    /// The <c>role</c> column.
    /// </summary>
    public RoleName Role { get; set; }

    /// <summary>
    /// The <c>permission</c> column.
    /// </summary>
    public Permission Permission { get; set; }
}
