using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The domains an organization locks its members' email addresses to: listed,
/// verified by a DNS TXT record and removed under <c>domain:manage</c> in the
/// administrative organization.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, REG-DOM-001, IDN-ORG-006 and chapter 09 section 8a. The list
/// is the policy field <c>emailDomains</c>, written here and nowhere else. Every change
/// carries a reason, is audited and is the <c>domain:manage</c> step-up action; listing
/// a domain and verifying one admit more addresses, so each is a loosening and also
/// asks <c>system:administer</c>. Removing one stops new sign-ins with addresses in it
/// and raises <c>domain-removed</c>.
/// </remarks>
public interface IOrganizationDomains
{
    /// <summary>
    /// Reads every domain an organization lists, with where its verification stands.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The domains in the order they were listed, or the refusal:
    /// <c>api.request.malformed</c> naming <c>id</c> where the deployment holds no such
    /// organization.
    /// </returns>
    ValueTask<Result<IReadOnlyList<OrganizationDomain>>> DomainsAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists a domain, unverified and admitting nothing, and draws the token its TXT
    /// record must carry. Listing one already listed changes nothing and answers it as
    /// it stands.
    /// </summary>
    /// <param name="context">Who is listing it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="domain">The domain, in either its ASCII or its Unicode form.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The domain and the record to publish, or the refusal: <c>api.request.malformed</c>
    /// naming <c>id</c>, <c>domain</c> or <c>reason</c>.
    /// </returns>
    ValueTask<Result<OrganizationDomain>> AddDomainAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        string domain,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Looks for a listed domain's TXT record and, where it carries the token, verifies
    /// the domain, which then admits addresses in it. Verifying one already verified
    /// changes nothing.
    /// </summary>
    /// <param name="context">Who is verifying it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The domain as it now stands, or the refusal: <c>identity.domain.unverified</c>
    /// where no record carries the token or none could be read,
    /// <c>api.request.malformed</c> naming <c>id</c>, <c>domain</c> or <c>reason</c>.
    /// </returns>
    ValueTask<Result<OrganizationDomain>> VerifyDomainAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        string domain,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a domain from the list: new sign-ins with addresses in it stop, and the
    /// removal raises <c>domain-removed</c>. Its token is never drawn again. Removing a
    /// domain the organization does not list changes nothing.
    /// </summary>
    /// <param name="context">Who is removing it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>api.request.malformed</c> naming <c>id</c>,
    /// <c>domain</c> or <c>reason</c>.
    /// </returns>
    ValueTask<Result> RemoveDomainAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        string domain,
        string reason,
        CancellationToken cancellationToken);
}
