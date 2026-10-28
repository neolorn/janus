using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The operations over person records: what staff do to a record of someone who holds
/// no account.
/// </summary>
/// <remarks>
/// Implements LIB-API-005 and chapter 21. Each is asked of the access gate, and a
/// refusal keeps nothing, so each may be called inside the host's unit of work.
/// </remarks>
public interface IPersonRecords
{
    /// <summary>
    /// Enters a record of origin <c>staff</c> for someone who gave their details
    /// otherwise, under <c>record:create</c> in the organization the entry names. Its
    /// contacts are held unverified, so no code reaches it and no account is offered it.
    /// </summary>
    /// <param name="context">Which staff member is entering it.</param>
    /// <param name="entry">What the person gave.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The record's subject, or the refusal: <c>authz.denied</c> where the caller does
    /// not hold the permission in the organization; <c>api.request.invalid</c> naming the
    /// member for an organization the deployment does not hold or whose deletion is
    /// requested, or a language it does not send in; <c>api.request.malformed</c> naming
    /// <c>reference</c> outside its bounds; <c>identity.identifier.invalid</c> for an
    /// email or phone that is not well formed; <c>identity.profile.invalid</c> for a name
    /// outside its rule; <c>privacy.purpose.noconsent</c> for a purpose that does not rest
    /// on consent; <c>privacy.notice.unpublished</c> for a consent whose governing
    /// document has no published version.
    /// </returns>
    /// <exception cref="ArgumentNullException">The context or the entry is absent.</exception>
    ValueTask<Result<SubjectId>> EnterAsync(
        AccessContext context,
        PersonRecordEntry entry,
        CancellationToken cancellationToken);
}
