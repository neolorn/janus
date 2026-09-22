using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Privacy.Records;

namespace Janus.Storage.Privacy.Records;

/// <summary>
/// The roles as the records of processing read them, over the authorization area's
/// own store.
/// </summary>
/// <param name="roles">Where the roles are.</param>
/// <remarks>
/// Implements PRIV-ROPA-001 and CONV-DESIGN-003. The register asks one question of
/// the roles and gets one answer: nothing about grants, subjects or resources crosses
/// this port.
/// </remarks>
internal sealed class RegisterRoles(IRoleStore roles) : IRegisterRoles
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<string, IReadOnlyList<Permission>>> AllowedAsync(
        CancellationToken cancellationToken) =>
        (await roles.AllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(
                role => role.Name.ToString(),
                role => (IReadOnlyList<Permission>)[.. role.Permissions]);
}
