using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The staff search: accounts and person records found by part of a name, an email or a
/// phone, typed loosely, over an index the serving process holds in its own memory.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-001 to IDN-SRCH-003 and LIB-API-005 (D-256). A search is asked
/// in an organization, under <c>contact:read</c> for accounts and <c>record:read</c> for
/// records, either held there or in the administrative organization; it writes no audit
/// row, counts its results toward the caller's read volume, and is held to
/// <c>search.perminute</c> searches a minute.
/// No host implements this interface: a host calls it, and a member the library adds to
/// it is an additive change (D-268).
/// </remarks>
public interface ISearch
{
    /// <summary>
    /// Finds the subjects the query matches, sorted by normalised display name and then
    /// by subject, a subject with no name last.
    /// </summary>
    /// <param name="context">Who is searching.</param>
    /// <param name="query">What is asked.</param>
    /// <param name="cancellationToken">Abandons the search.</param>
    /// <returns>
    /// The results, or the refusal: <c>api.request.malformed</c> naming <c>kinds</c> or
    /// <c>limit</c>; <c>api.request.invalid</c> naming <c>q</c> where the query
    /// normalises to nothing or to more than 64 characters;
    /// <c>identity.organization.notfound</c>; <c>authz.denied</c> where the caller may
    /// read none of the kinds asked, or is a system principal; <c>auth.throttled</c>
    /// past <c>search.perminute</c>; <c>identity.search.unavailable</c> where the
    /// process serves no index.
    /// </returns>
    ValueTask<Result<IReadOnlyList<SearchResult>>> SearchAsync(
        AccessContext context,
        SearchQuery query,
        CancellationToken cancellationToken);
}
