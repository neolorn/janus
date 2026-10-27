using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Authentication.Policies;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Storage.Authentication.Organizations;

/// <summary>
/// The admission of a read of an organization, judged by the gate.
/// </summary>
/// <param name="admission">The gate's admission of a read of an organization.</param>
/// <param name="memberships">Which organizations a person is a current member of.</param>
/// <remarks>Implements IDN-READ-001, IDN-READ-004 and CONV-LAYOUT-001 (D-219).</remarks>
internal sealed class ReadAdmission(IOrganizationReadAdmission admission, IMembershipLookup memberships)
    : IReadAdmission
{
    /// <inheritdoc/>
    public async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await admission.AdmitAsync(context, permission, organization, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<ReadableOrganizations>> ListedAsync(
        AccessContext context,
        Permission permission,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // IDN-READ-001 (D-219): a person lists among the organizations of their current
        // memberships; a job has none, and lists its own.
        IReadOnlyList<OrganizationId> held = context.Effective is SubjectId person
            ? await memberships.OfAsync(person, cancellationToken).ConfigureAwait(false)
            : [];

        return (await admission.ListedAsync(context, permission, held, cancellationToken).ConfigureAwait(false))
            .Match(
                listed => Result.Success(new ReadableOrganizations(listed.Every, listed.Organizations)),
                Result.Failure<ReadableOrganizations>);
    }
}
