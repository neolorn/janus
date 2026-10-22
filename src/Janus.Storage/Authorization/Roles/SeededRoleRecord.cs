using Janus.Core;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// The <c>seeded_roles</c> row.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004 (D-204). The row outlives the role it names, so a role an
/// administrator changed or removed is never seeded again.
/// </remarks>
internal sealed class SeededRoleRecord
{
    /// <summary>
    /// The <c>name</c> column, which is this table's key: the declared role's name.
    /// </summary>
    public RoleName Name { get; set; }
}
