using Janus.Core;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// The <c>seeded_role_inclusions</c> row.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-006 (D-278). The row outlives the inclusion it names, so an
/// inclusion an administrator changed or removed is never seeded again.
/// </remarks>
internal sealed class SeededRoleInclusionRecord
{
    /// <summary>
    /// The <c>role</c> column: the including role, as declared.
    /// </summary>
    public RoleName Role { get; set; }

    /// <summary>
    /// The <c>included</c> column: the role it includes, as declared.
    /// </summary>
    public RoleName Included { get; set; }
}
