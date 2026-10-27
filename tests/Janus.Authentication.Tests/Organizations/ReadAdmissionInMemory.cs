using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Authentication.Tests.Policies;
using Janus.Core;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// The read admission over a test's gate: a read naming an organization is admitted where
/// the permission is held there or in the administrative organization, and the list is
/// every organization to a holder in the administrative organization, the caller's
/// current memberships it is held in to anyone else, and a job's own organization
/// (IDN-READ-001, IDN-READ-004, D-219).
/// </summary>
/// <param name="gate">The test's gate, which holds what a test granted and records refusals.</param>
/// <param name="administrative">The organization that administers the deployment.</param>
/// <param name="memberships">Which organizations a person is a current member of.</param>
/// <remarks>
/// CONV-TEST-004: a fake that answers as the rule answers, not a recorder of calls. A
/// refusal is the gate's single check in the organization the path names, or in the
/// administrative organization for the list, so it is recorded once.
/// </remarks>
internal sealed class ReadAdmissionInMemory(
    AccessGateInMemory gate,
    AdministrativeOrganizationInMemory administrative,
    MembershipLookupInMemory memberships) : IReadAdmission
{
    /// <inheritdoc/>
    public async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (administrative.Organization is OrganizationId administering
            && Admits(context, permission, administering))
        {
            return null;
        }

        return (await gate.RequireAsync(context, permission, organization, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<ReadableOrganizations>> ListedAsync(
        AccessContext context,
        Permission permission,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (administrative.Organization is not OrganizationId administering)
        {
            return Result.Failure<ReadableOrganizations>(Error.From(ErrorCodes.Denied));
        }

        if (!context.IsSystem && Admits(context, permission, administering))
        {
            return Result.Success(new ReadableOrganizations(Every: true, []));
        }

        IEnumerable<OrganizationId> candidates = context.Principal?.Organization is OrganizationId own
            ? [own]
            : context.Effective is SubjectId person
                ? (await memberships.OfAsync(person, cancellationToken).ConfigureAwait(false))
                    .Distinct()
                    .OrderBy(candidate => candidate.Value)
                : [];
        List<OrganizationId> listed = [.. candidates.Where(candidate => Admits(context, permission, candidate))];

        if (listed.Count > 0)
        {
            return Result.Success(new ReadableOrganizations(Every: false, listed));
        }

        return (await gate.RequireAsync(context, permission, administering, cancellationToken).ConfigureAwait(false))
            .Match(
                () => Result.Success(new ReadableOrganizations(Every: false, [administering])),
                Result.Failure<ReadableOrganizations>);
    }

    // A job is admitted in its own organization, where its operation runs; a person
    // where a test granted the permission.
    private bool Admits(AccessContext context, Permission permission, OrganizationId organization) =>
        context.Principal is SystemPrincipal principal
            ? principal.ActsFor(organization)
            : context.Effective is SubjectId subject && gate.Holds(subject, organization, permission);
}
