using Janus.Core;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// The <c>roles</c> row.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004 and CONV-DESIGN-003. A role is a row rather than a type,
/// which is what lets one be added and edited without a deployment.
/// </remarks>
internal sealed class RoleRecord
{
    /// <summary>
    /// The <c>name</c> column, which is this table's key and what a grant names.
    /// </summary>
    public RoleName Name { get; set; }
}
