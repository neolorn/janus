using System.Collections.Frozen;
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
    /// Every derivation the host's own rows decide that reaches records of the type,
    /// with what the role it confers allows.
    /// </summary>
    /// <param name="type">The kind of thing the records are.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// One entry per reaching derivation, a materialised one left out. What the role
    /// allows is read here and mapped in memory, so a page costs no query per
    /// permission (AUTHZ-GATE-005 AC1).
    /// </returns>
    public async ValueTask<IReadOnlyList<ConferredDerivation>> ConferringAsync(
        ResourceType type,
        CancellationToken cancellationToken)
    {
        var conferring = new List<ConferredDerivation>();

        foreach (ReachingDerivation each in model.Derivations(type))
        {
            if (each.Derivation.Materialised)
            {
                continue;
            }

            Role? role = await roles.FindAsync(each.Derivation.Role, cancellationToken)
                .ConfigureAwait(false);

            conferring.Add(new ConferredDerivation(
                each.Relationship,
                each.Derivation.Role,
                role is null ? FrozenSet<Permission>.Empty : role.Permissions));
        }

        return conferring;
    }

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
