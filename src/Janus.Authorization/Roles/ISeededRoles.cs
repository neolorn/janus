using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Roles;

/// <summary>
/// The names of the declared roles a start has seeded, each kept so that no later start
/// seeds it again.
/// </summary>
/// <remarks>Implements AUTHZ-GRANT-004 and CONV-DESIGN-003 (D-204).</remarks>
internal interface ISeededRoles
{
    /// <summary>
    /// Holds a declared role's name until the operation's transaction ends, so two
    /// starts seeding it at once seed it one after the other and the second finds it
    /// seeded (CONV-DESIGN-003).
    /// </summary>
    /// <param name="name">The declared role's name.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking the lock.</returns>
    /// <exception cref="System.InvalidOperationException">No transaction is open.</exception>
    ValueTask HoldAsync(RoleName name, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a start has seeded the declared role of this name.
    /// </summary>
    /// <param name="name">The declared role's name.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether one has.</returns>
    ValueTask<bool> SeededAsync(RoleName name, CancellationToken cancellationToken);

    /// <summary>
    /// Keeps the declared role's name as seeded.
    /// </summary>
    /// <param name="name">The declared role's name, not yet kept.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(RoleName name, CancellationToken cancellationToken);
}
