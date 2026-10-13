using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Tests.Invitations;
using Janus.Authorization.Roles;
using Janus.Authorization.Tests.Gate;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// Whether anything names a role, read from the grants and the invitations held in
/// memory, as the database reads the two tables.
/// </summary>
/// <param name="grants">The grants held.</param>
/// <param name="invitations">The invitations held.</param>
/// <remarks>
/// CONV-TEST-004: a fake over the other fakes, so a grant or an invitation a test
/// writes names its role here as the rows name it.
/// </remarks>
internal sealed class RoleReferencesInMemory(GrantsInMemory grants, InvitationStoreInMemory invitations)
    : IRoleReferences
{
    /// <inheritdoc/>
    public ValueTask<bool> NamedAsync(RoleName role, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            grants.Names(role)
            || invitations.Held.Any(invitation => invitation.Stands && invitation.Roles.Contains(role)));
}
