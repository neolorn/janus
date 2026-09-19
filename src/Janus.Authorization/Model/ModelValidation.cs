using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Janus.Core;

namespace Janus.Authorization.Model;

/// <summary>
/// The two checks of the model that the declaration alone cannot decide: what the
/// stored roles allow, and whether the columns a derivation names can be looked up.
/// </summary>
/// <param name="model">The host's declared domain.</param>
/// <param name="roles">Where the roles a deployment wrote are read.</param>
/// <param name="indexes">Where the database's own catalogue is read.</param>
/// <remarks>
/// Implements AUTHZ-MODEL-004 and AUTHZ-DERIVE-004. Roles are written at runtime
/// (AUTHZ-GRANT-004) and the host's relations are the host's, so neither is in the
/// declaration; both are read here, before a request is served (D-160).
/// </remarks>
internal sealed class ModelValidation(
    AuthorizationModel model,
    IRoleStore roles,
    IIndexCatalogue indexes)
{
    /// <summary>
    /// Runs both checks, raising the first failure.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of running them.</returns>
    /// <exception cref="StartupException">
    /// A stored role allows a permission the model does not declare, or a derivation
    /// names a column no index reaches.
    /// </exception>
    public async ValueTask ValidateAsync(CancellationToken cancellationToken)
    {
        await RolesAsync(cancellationToken).ConfigureAwait(false);
        await DerivationsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RolesAsync(CancellationToken cancellationToken)
    {
        foreach (Role role in await roles.AllAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (Permission permission in role.Permissions)
            {
                if (!model.Declares(permission))
                {
                    throw AuthorizationModel.Refused(
                        ErrorCodes.StartupUndeclaredPermission,
                        "role",
                        role.Name.ToString(),
                        "it allows " + permission + ", which the model does not declare");
                }
            }
        }
    }

    private async ValueTask DerivationsAsync(CancellationToken cancellationToken)
    {
        foreach (ResourceTypeDeclaration type in model.ResourceTypes)
        {
            foreach (DerivationDeclaration derivation in type.Derivations)
            {
                RelationshipDeclaration relationship =
                    model.Relationship(derivation.Relationship)
                    ?? throw new InvalidOperationException(
                        "The derivation follows from '" + derivation.Relationship
                        + "', which the model does not declare as a relationship.");

                IReadOnlySet<string> indexed = await indexes
                    .IndexedAsync(relationship.Relation, cancellationToken)
                    .ConfigureAwait(false);

                foreach (string column in relationship.Columns)
                {
                    if (!indexed.Contains(column))
                    {
                        throw AuthorizationModel.Refused(
                            ErrorCodes.StartupUnindexedDerivation,
                            "relationship",
                            relationship.Name,
                            "its evaluation depends on " + relationship.Relation + "." + column
                            + ", which no index reaches");
                    }
                }
            }
        }
    }
}
