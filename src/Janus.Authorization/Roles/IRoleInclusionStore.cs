using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Roles;

/// <summary>
/// Where roles' inclusions of one another are read and written, apart from the roles'
/// own permissions and <c>handsOut</c>.
/// </summary>
/// <remarks>Implements AUTHZ-GRANT-006 and CONV-DESIGN-003 (D-278).</remarks>
internal interface IRoleInclusionStore
{
    /// <summary>
    /// Holds the deployment's inclusions until the operation's transaction ends, so two
    /// changes that would close a loop between them, or break the rule on grants that
    /// must end, are made one after the other and the second is judged on what the first
    /// left (CONV-DESIGN-003).
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking the lock.</returns>
    /// <exception cref="System.InvalidOperationException">No transaction is open.</exception>
    ValueTask HoldAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads every inclusion, on or off.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Each inclusion with the role that includes.</returns>
    ValueTask<IReadOnlyList<(RoleName Role, RoleInclusion Inclusion)>> AllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new inclusion.
    /// </summary>
    /// <param name="role">The including role.</param>
    /// <param name="inclusion">The inclusion.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CreateAsync(RoleName role, RoleInclusion inclusion, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the inclusion as it now stands onto its row.
    /// </summary>
    /// <param name="role">The including role.</param>
    /// <param name="inclusion">The inclusion as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="System.InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(RoleName role, RoleInclusion inclusion, CancellationToken cancellationToken);

    /// <summary>
    /// Removes an inclusion.
    /// </summary>
    /// <param name="role">The including role.</param>
    /// <param name="included">The role it includes.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(RoleName role, RoleName included, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the host's actions declared not inherited as this start declares them, in
    /// place of what an earlier start wrote, so the view a grant is read through passes
    /// none of them on.
    /// </summary>
    /// <param name="permissions">The actions declared not inherited.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording them.</returns>
    ValueTask DeclareUninheritedAsync(IReadOnlySet<Permission> permissions, CancellationToken cancellationToken);
}
