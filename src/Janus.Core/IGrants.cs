using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Stored grants written and revoked at runtime, under <c>grant:manage</c> in the
/// organization the grant is scoped to.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GRANT-001 to AUTHZ-GRANT-004, OPS-CFG-007 and chapter
/// 09 section 8. Both are step-up actions; a grant or revocation of a role carrying
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
}
