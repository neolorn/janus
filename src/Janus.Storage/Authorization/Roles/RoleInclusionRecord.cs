using Janus.Core;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// The <c>role_inclusions</c> row: one role's inclusion of another.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-006 (D-278). It is kept apart from the role's own permissions
/// and <c>handsOut</c>, so switching an inclusion off, or every inclusion through
/// <c>authz.roleinclusion.enabled</c>, changes neither and switching back restores what
/// it gave.
/// </remarks>
internal sealed class RoleInclusionRecord
{
    /// <summary>
    /// The <c>role</c> column: the including role.
    /// </summary>
    public RoleName Role { get; set; }

    /// <summary>
    /// The <c>included</c> column: the role it includes.
    /// </summary>
    public RoleName Included { get; set; }

    /// <summary>
    /// The <c>enabled</c> column: whether the inclusion admits.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The <c>left_out_permissions</c> column: the permissions it does not pass on, by
    /// name, in one order.
    /// </summary>
    public string[] LeftOutPermissions { get; set; } = [];

    /// <summary>
    /// The <c>left_out_hands_out</c> column: the roles of the included role's
    /// <c>handsOut</c> it does not pass on, by name, in one order.
    /// </summary>
    public string[] LeftOutHandsOut { get; set; } = [];
}
