using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The roles grants confer, read, defined and removed at runtime under
/// <c>role:manage</c> in the administrative organization, and their inclusions of one
/// another made, changed and removed under <c>roleinclusion:manage</c> there (D-278).
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GRANT-004, OPS-CFG-007 and chapter 09 section 8.
/// Defining and removing are the <c>grant:manage</c> step-up action and carry a
/// reason; a change to a role that carries <c>system:administer</c>, before or after,
/// also needs that permission. Every change is audited with what the role was and what
/// it became.
/// No host implements this interface: a host calls it, and a member the library adds to
/// it is an additive change (D-268).
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
    /// Success, or the refusal: <c>authz.role.inuse</c> where a grant, a derivation or
    /// a standing invitation names it, <c>authz.role.notfound</c> where the deployment
    /// holds no such role.
    /// </returns>
    ValueTask<Result> RemoveAsync(
        AccessContext context,
        SessionId session,
        RoleName role,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Makes a role include another, or sets the inclusion that exists to what is stated;
    /// the change reaches every holder of the role at the next request.
    /// </summary>
    /// <param name="context">Who is making it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="role">The including role.</param>
    /// <param name="inclusion">The inclusion as it is to stand.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the inclusion is new, or the refusal: <c>authz.role.notfound</c> where
    /// either role is not one the deployment holds; <c>authz.role.inclusionloop</c> where
    /// the inclusion would make a role include itself; <c>authz.role.inclusionmustend</c>
    /// where a role whose grants must end would be included by one whose grants need not
    /// end; <c>authz.role.notassignable</c> where the change reaches beyond the caller;
    /// <c>api.request.malformed</c> naming <c>leftOut</c> where a permission it leaves out
    /// is not declared.
    /// </returns>
    /// <remarks>
    /// Implements AUTHZ-GRANT-006 (D-278), under <c>roleinclusion:manage</c> in the
    /// administrative organization.
    /// </remarks>
    ValueTask<Result<bool>> IncludeAsync(
        AccessContext context,
        SessionId session,
        RoleName role,
        RoleInclusion inclusion,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a role's inclusion of another; the change reaches every holder of the role
    /// at the next request.
    /// </summary>
    /// <param name="context">Who is removing it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="role">The including role.</param>
    /// <param name="included">The role it includes.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>authz.role.notfound</c> where either role is not one
    /// the deployment holds or the role does not include the other;
    /// <c>authz.role.notassignable</c> where the change reaches beyond the caller.
    /// </returns>
    /// <remarks>
    /// Implements AUTHZ-GRANT-006 (D-278), under <c>roleinclusion:manage</c> in the
    /// administrative organization.
    /// </remarks>
    ValueTask<Result> RemoveInclusionAsync(
        AccessContext context,
        SessionId session,
        RoleName role,
        RoleName included,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// The roles the caller's own account holds in its own name in an organization,
    /// through its live allow grants there, each once, oldest grant first.
    /// </summary>
    /// <param name="context">Who is asking, whose own account is read.</param>
    /// <param name="organization">The organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Each role with what it carries and hands out through its inclusions and the roles
    /// that may give it; none where the account holds none there, an organization the
    /// deployment does not hold included; <c>authz.denied</c> for a context that is
    /// effective for no account.
    /// </returns>
    /// <remarks>
    /// Implements AUTHZ-GRANT-006 (D-280). It asks no permission and records nothing; a
    /// restricted account is answered its own (IDN-ACCT-007). A role reaching the account
    /// through a group is the group's and is not answered.
    /// </remarks>
    ValueTask<Result<IReadOnlyList<HeldRole>>> OwnAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken);
}
