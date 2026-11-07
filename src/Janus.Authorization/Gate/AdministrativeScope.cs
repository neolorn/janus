using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Whether the caller may perform an operation of the deployment rather than of one
/// organization's records.
/// </summary>
/// <param name="gate">The one place a permission is evaluated.</param>
/// <param name="administrative">Which organization administers the deployment.</param>
/// <remarks>
/// Implements AUTHZ-SEAM-001, AUTHZ-SCOPE-001, AUTHZ-CONCEAL-005 and LIB-API-005. The
/// deployment is the administrative organization's to administer, so the permission is
/// asked there and nowhere else, and calling the service in process is no way round it.
/// </remarks>
internal sealed class AdministrativeScope(IAccessGate gate, IAdministrativeOrganization administrative)
{
    // The type the gate asks an organization-wide question on.
    private static readonly ResourceType OrganizationWide = ResourceType.Parse("organization");

    /// <summary>
    /// The refusal, or nothing where the caller holds the permission in the
    /// administrative organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The refusal, or nothing.</returns>
    public async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        Permission permission,
        CancellationToken cancellationToken)
    {
        if (await administrative.FindAsync(cancellationToken).ConfigureAwait(false)
            is not OrganizationId organization)
        {
            return Error.From(ErrorCodes.Denied);
        }

        return (await gate
                .RequireAsync(context, permission, organization, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
    }

    /// <summary>
    /// The refusal, or nothing where the caller holds any of the permissions in the
    /// administrative organization: a read open to each of them, admitted as one
    /// admission, so a permission the caller lacks is not recorded as refused where
    /// another admits the read.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permissions">The permissions any of which admits the read, at least one.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The refusal of the first, recorded once, or nothing.</returns>
    /// <remarks>
    /// What the caller may do across the organization is read first, recording nothing
    /// (BFF-CAP-002); a permission held with nothing outstanding admits the read, and
    /// where none is, the first is asked of the single check, which records its refusal.
    /// </remarks>
    public async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        if (await administrative.FindAsync(cancellationToken).ConfigureAwait(false)
            is not OrganizationId organization)
        {
            return Error.From(ErrorCodes.Denied);
        }

        if ((await gate
                .CapabilitiesAsync(
                    context,
                    OrganizationWide,
                    [ResourceId.Parse(organization.ToString())],
                    permissions,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(
                capabilities => capabilities is [Capability held]
                    && permissions.Any(permission => held.Can.Contains(permission) && !held.Requires.ContainsKey(permission)),
                _ => false))
        {
            return null;
        }

        return (await gate
                .RequireAsync(context, permissions[0], organization, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
    }
}
