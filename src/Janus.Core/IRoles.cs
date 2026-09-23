using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The roles grants confer, read, defined and removed at runtime under
/// <c>role:manage</c> in the administrative organization.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GRANT-004, OPS-CFG-007 and chapter 09 section 8.
/// Defining and removing are the <c>grant:manage</c> step-up action and carry a
/// reason; a change to a role that carries <c>system:administer</c>, before or after,
/// also needs that permission. Every change is audited with what the role was and what
/// it became.
/// </remarks>
public interface IRoles
{
    /// <summary>
    /// Every role the deployment holds, with its permissions.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The roles by name, or the refusal.</returns>
    ValueTask<Result<IReadOnlyList<DefinedRole>>> AllAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a role, or gives the one by that name the permissions stated; the change
    /// takes effect on the next request.
    /// </summary>
    /// <param name="context">Who is defining it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="role">The role as it is to stand.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the role is new, or the refusal: <c>api.request.malformed</c> naming
    /// <c>permissions</c> where one of them is not declared.
    /// </returns>
    ValueTask<Result<bool>> DefineAsync(
        AccessContext context,
        SessionId session,
        DefinedRole role,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a role nothing names.
    /// </summary>
    /// <param name="context">Who is removing it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="role">Which role.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>authz.role.inuse</c> where a grant or a derivation
    /// names it, <c>api.request.malformed</c> naming <c>name</c> where the deployment
    /// holds no such role.
    /// </returns>
    ValueTask<Result> RemoveAsync(
        AccessContext context,
        SessionId session,
        RoleName role,
        string reason,
        CancellationToken cancellationToken);
}
