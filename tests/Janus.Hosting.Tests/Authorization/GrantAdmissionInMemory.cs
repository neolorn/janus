using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Tests;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The grant admission over a test deployment's gate: <c>system:administer</c> held in
/// the administrative organization admits a grant in another, and otherwise the gate's
/// check of <c>grant:manage</c> in the grant's organization answers, so a refusal is kept
/// once (AUTHZ-SCOPE-001, D-208).
/// </summary>
/// <param name="gate">The deployment's gate, which holds what a test granted.</param>
/// <remarks>
/// CONV-TEST-004: a fake that answers as the admission answers, not a recorder of calls.
/// </remarks>
internal sealed class GrantAdmissionInMemory(AccessGateInMemory gate) : IGrantAdmission
{
    /// <inheritdoc/>
    public async ValueTask<Result> AdmitAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return gate.Administrative is OrganizationId administrative
            && administrative != organization
            && gate.Admits(context, Permissions.SystemAdminister, administrative)
                ? await gate.RequireAsync(context, Permissions.SystemAdminister, administrative, cancellationToken)
                : await gate.RequireAsync(context, Permissions.GrantManage, organization, cancellationToken);
    }
}
