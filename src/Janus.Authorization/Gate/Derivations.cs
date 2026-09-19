using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Model;
using Janus.Authorization.Roles;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Which of the host's relationships confer one of the permissions being asked for on
/// records of a resource type.
/// </summary>
/// <param name="model">The host's declaration, read for what follows from what.</param>
/// <param name="roles">Where what a role allows is read.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-001, AUTHZ-DERIVE-002 and AUTHZ-DERIVE-005. What the
/// conferred role allows is read where the derivation is evaluated, so editing the
/// role decides the next request (AUTHZ-CACHE-001). A materialised derivation is not
/// evaluated here: its rows are grants, and the grants are read as grants.
/// </remarks>
internal sealed class Derivations(AuthorizationModel model, IRoleStore roles)
{
    /// <summary>
    /// The relationships whose derivations confer one of the permissions on records of
    /// the type, those declared on a type containing it included.
    /// </summary>
    /// <param name="type">The kind of thing the records are.</param>
    /// <param name="permissions">What is being asked for.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The relationships, which is nothing where none confers one.</returns>
    public async ValueTask<IReadOnlyList<RelationshipDeclaration>> ReachingAsync(
        ResourceType type,
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ReachingDerivation> reaching = model.Derivations(type);

        if (reaching.Count == 0)
        {
            return [];
        }

        var relationships = new List<RelationshipDeclaration>();

        foreach (ReachingDerivation each in reaching)
        {
            if (each.Derivation.Materialised)
            {
                continue;
            }

            Role? role = await roles.FindAsync(each.Derivation.Role, cancellationToken)
                .ConfigureAwait(false);

            if (role is not null && permissions.Any(role.Permissions.Contains))
            {
                relationships.Add(each.Relationship);
            }
        }

        return relationships;
    }
}
