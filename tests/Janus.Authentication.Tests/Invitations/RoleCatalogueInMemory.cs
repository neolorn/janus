using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Core;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// The roles a deployment defines, held in memory.
/// </summary>
internal sealed class RoleCatalogueInMemory : IRoleCatalogue
{
    private readonly Dictionary<RoleName, DefinedRole> _roles = [];

    /// <summary>
    /// Defines a role.
    /// </summary>
    /// <param name="name">The role.</param>
    /// <param name="permissions">What it permits.</param>
    /// <returns>The role's name.</returns>
    public RoleName Define(string name, params Permission[] permissions)
    {
        var role = RoleName.Parse(name);

        _roles[role] = new DefinedRole(role, permissions);

        return role;
    }

    /// <inheritdoc/>
    public ValueTask<DefinedRole?> FindAsync(RoleName name, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_roles.GetValueOrDefault(name));
}
