using System;
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
/// this port. What each role allows is what a holder of it may do, through its
/// inclusions (AUTHZ-GRANT-006, D-278).
/// </remarks>
internal sealed class RegisterRoles(IRoleStore roles) : IRegisterRoles
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<string, IReadOnlyList<Permission>>> AllowedAsync(
        CancellationToken cancellationToken)
    {
        var allowed = new Dictionary<string, IReadOnlyList<Permission>>(StringComparer.Ordinal);

        foreach (Role role in await roles.AllAsync(cancellationToken).ConfigureAwait(false))
        {
            // A role removed between the two reads allows nothing, so it is not reported.
            if (await roles.EffectiveAsync(role.Name, cancellationToken).ConfigureAwait(false) is Role effective)
            {
                allowed[role.Name.ToString()] =
                [
                    .. effective.Permissions.OrderBy(permission => permission.ToString(), StringComparer.Ordinal),
                ];
            }
        }

        return allowed;
    }
}
