using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Stored grants written and revoked at runtime, under <c>grant:manage</c> in the
/// organization the grant is scoped to, and read under <c>grant:read</c> there.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GRANT-001 to AUTHZ-GRANT-004, OPS-CFG-007, chapter 09
/// section 8 and chapter 16 section 3 step 4 (entry 268). Writing and revoking are
/// step-up actions; a grant or revocation of a role carrying
/// <c>system:administer</c> also needs that permission in the administrative
/// organization, and every one records who, when and why.
/// </remarks>
public interface IGrants
{
    /// <summary>
    /// Writes a grant, which takes effect on the next request.
    /// </summary>
    /// <param name="context">Who is granting.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="request">What is granted, to whom, on what and why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The grant's identifier, or the refusal: <c>authz.grant.duplicate</c> where an
    /// identical live grant exists, <c>authz.grant.expired</c> where the expiry has
    /// passed already, <c>authz.grant.reasonrequired</c> where the reason is blank.
    /// </returns>
    ValueTask<Result<GrantId>> GrantAsync(
        AccessContext context,
        SessionId session,
        GrantRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revokes a stored grant, recording who revoked it and why; the revocation takes
    /// effect on the next request.
    /// </summary>
    /// <param name="context">Who is revoking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="grant">Which grant.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>authz.grant.notfound</c> where no stored grant by
    /// that identifier stands unrevoked, <c>authz.grant.reasonrequired</c> where the
    /// reason is blank.
    /// </returns>
    ValueTask<Result> RevokeAsync(
        AccessContext context,
        SessionId session,
        GrantId grant,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// The live grants one user or group holds in its own name within an organization,
    /// oldest first; a grant reaching it through a group it belongs to is the group's.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">The organization the grants are scoped to.</param>
    /// <param name="holder">The user or group.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The grants, or <c>authz.denied</c> where the caller does not hold
    /// <c>grant:read</c> in the organization.
    /// </returns>
    ValueTask<Result<IReadOnlyList<HeldGrant>>> HeldAsync(
        AccessContext context,
        OrganizationId organization,
        GrantSubject holder,
        CancellationToken cancellationToken);
}
