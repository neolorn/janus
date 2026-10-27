using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Tests.Invitations;
using Janus.Authorization.Roles;
using Janus.Authorization.Tests.Gate;
using Janus.Authorization.Tests.Roles;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// Whether anything names a role, read from the grants, the invitations and the other
/// roles held in memory, as the database reads the three tables.
/// </summary>
/// <param name="grants">The grants held.</param>
/// <param name="invitations">The invitations held.</param>
/// <param name="roles">The roles held.</param>
/// <remarks>
/// CONV-TEST-004: a fake over the other fakes, so a grant, an invitation or a role a
/// test writes names its role here as the rows name it.
/// </remarks>
internal sealed class RoleReferencesInMemory(
    GrantsInMemory grants,
    InvitationStoreInMemory invitations,
    RolesInMemory roles) : IRoleReferences
{
    /// <inheritdoc/>
    public async ValueTask<bool> NamedAsync(RoleName role, CancellationToken cancellationToken)
    {
        IReadOnlyList<Role> held = await roles.AllAsync(cancellationToken).ConfigureAwait(false);

        return grants.Names(role)
            || invitations.Held.Any(invitation => invitation.Stands && invitation.Roles.Contains(role))
            || held.Any(other => other.Name != role && other.HandsOut.Contains(role));
    }
}
