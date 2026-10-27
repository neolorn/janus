using Janus.Core;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// The <c>roles</c> row.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004, AUTHZ-GRANT-005 and CONV-DESIGN-003. A role is a row
/// rather than a type, which is what lets one be added and edited without a deployment.
/// </remarks>
internal sealed class RoleRecord
{
    /// <summary>
    /// The <c>name</c> column, which is this table's key and what a grant names.
    /// </summary>
    public RoleName Name { get; set; }

    /// <summary>
    /// The <c>hands_out</c> column: the roles a holder may hand out, by name, as a
    /// declaration or an administrator wrote them (D-212).
    /// </summary>
    public string[] HandsOut { get; set; } = [];
}
