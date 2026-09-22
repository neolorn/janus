using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Records;

/// <summary>
/// Which roles hold which permissions, which is the one thing the records of
/// processing need from the authorization area.
/// </summary>
/// <remarks>
/// Implements PRIV-ROPA-001 and CONV-LAYOUT-001. The organisational roles with access
/// to a purpose are the roles holding a permission declared to serve it, so the
/// register reads the roles as they stand rather than a list anyone maintains.
/// </remarks>
internal interface IRegisterRoles
{
    /// <summary>
    /// Every role the deployment holds, with the permissions each allows.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The permissions each role allows, by the role's name.</returns>
    ValueTask<IReadOnlyDictionary<string, IReadOnlyList<Permission>>> AllowedAsync(
        CancellationToken cancellationToken);
}
