using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Janus.Core;

namespace Janus.Authorization.Tests.Roles;

/// <summary>
/// The roles a deployment wrote, held in memory.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that answers from what it holds, so a test that writes a role
/// sees what the startup check would see.
/// </remarks>
internal sealed class RolesInMemory : IRoleStore
{
    private readonly Dictionary<RoleName, Role> _roles = [];

    /// <inheritdoc/>
    public ValueTask<Role?> FindAsync(RoleName name, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_roles.TryGetValue(name, out Role? role) ? role : null);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Role>> AllAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Role>>([.. _roles.Values.OrderBy(role => role.Name)]);

    /// <inheritdoc/>
    public ValueTask CreateAsync(Role role, CancellationToken cancellationToken)
    {
        _roles.Add(role.Name, role);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(Role role, CancellationToken cancellationToken)
    {
        _roles[role.Name] = role;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(RoleName name, CancellationToken cancellationToken)
    {
        _roles.Remove(name);

        return ValueTask.CompletedTask;
    }
}
