using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Roles;

/// <summary>
/// Where roles are read and written.
/// </summary>
/// <remarks>Implements AUTHZ-GRANT-004 and CONV-DESIGN-003.</remarks>
internal interface IRoleStore
{
    /// <summary>
    /// Reads one role.
    /// </summary>
    /// <param name="name">Which role.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The role, or nothing where no such row exists.</returns>
    ValueTask<Role?> FindAsync(RoleName name, CancellationToken cancellationToken);

    /// <summary>
    /// Reads every role, which is what startup checks against the declared
    /// permissions.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Every role.</returns>
    ValueTask<IReadOnlyList<Role>> AllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new role.
    /// </summary>
    /// <param name="role">The role to create.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CreateAsync(Role role, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the role as it now stands onto its rows.
    /// </summary>
    /// <param name="role">The role as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="System.InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(Role role, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a role, which is a definition rather than a record of something that
    /// happened (IDN-PRIN-003).
    /// </summary>
    /// <param name="name">Which role.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(RoleName name, CancellationToken cancellationToken);
}
