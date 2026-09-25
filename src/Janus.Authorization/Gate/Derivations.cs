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
/// <param name="suspensions">Where whether the organization is suspended is read.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-001, AUTHZ-DERIVE-002, AUTHZ-DERIVE-005 and IDN-ORG-003. What
/// the conferred role allows is read where the derivation is evaluated, so editing the
/// role decides the next request (AUTHZ-CACHE-001). A materialised derivation is not
/// evaluated here: its rows are grants, and the grants are read as grants. In a
/// suspended organization no derivation confers anything, as none of its grants does,
/// so materialising one changes no answer (AUTHZ-TEST-001 AC3, entry 266).
/// </remarks>
internal sealed class Derivations(
    AuthorizationModel model,
    IRoleStore roles,
    IOrganizationSuspensions suspensions)
{
    /// <summary>
    /// Every derivation the host's own rows decide that reaches records of the type
    /// in the organization, with what the role it confers allows.
    /// </summary>
    /// <param name="type">The kind of thing the records are.</param>
    /// <param name="organization">The organization the records sit in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// One entry per reaching derivation, a materialised one left out, and none while
    /// the organization is suspended. What the role allows is read here and mapped in
    /// memory, so a page costs no query per permission (AUTHZ-GATE-005 AC1).
    /// </returns>
    public async ValueTask<IReadOnlyList<ConferredDerivation>> ConferringAsync(
        ResourceType type,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        if (!await ConfersAsync(type, organization, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

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
    /// Whether a derivation the host's own rows decide reaches records of the type,
    /// those declared on a type containing it included.
    /// </summary>
    /// <param name="type">The kind of thing the records are.</param>
    /// <returns>
    /// Whether one reaches it, whatever the role it confers allows. A materialised
    /// derivation is left out: its grants are rows and are read as rows.
    /// </returns>
    public bool Reaches(ResourceType type) =>
        model.Derivations(type).Any(each => !each.Derivation.Materialised);

    /// <summary>
    /// The relationships whose derivations confer one of the permissions on records of
    /// the type in the organization, those declared on a type containing it included.
    /// </summary>
    /// <param name="type">The kind of thing the records are.</param>
    /// <param name="organization">The organization the records sit in.</param>
    /// <param name="permissions">What is being asked for.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The relationships, which is nothing where none confers one or the organization
    /// is suspended.
    /// </returns>
    public async ValueTask<IReadOnlyList<RelationshipDeclaration>> ReachingAsync(
        ResourceType type,
        OrganizationId organization,
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken)
    {
        if (!await ConfersAsync(type, organization, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var relationships = new List<RelationshipDeclaration>();

        foreach (ReachingDerivation each in model.Derivations(type))
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

    // IDN-ORG-003: "Organization suspends immediately; access stops", derived access
    // with it. The organization is read only where a derivation would be evaluated,
    // so a type none reaches costs no query.
    private async ValueTask<bool> ConfersAsync(
        ResourceType type,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        Reaches(type)
        && !await suspensions.IsSuspendedAsync(organization, cancellationToken).ConfigureAwait(false);
}
