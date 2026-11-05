using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The reach of a record access session: the subjects of the person records the browser
/// of the request being served reaches by a code, which a host shows its own data about.
/// </summary>
/// <remarks>
/// Implements REC-ACC-003 and LIB-API-005. A seam, not an operation: it has no endpoint,
/// takes no access context and joins no unit of work. What admits it is the record
/// access session the browser of the request being served holds, so it is called while
/// that request is served. It reads the reach at that moment and changes nothing: a host
/// that changes something on the strength of the reach asks again inside its change, and
/// answers a subject outside the reach exactly as one that does not exist.
/// </remarks>
public interface IRecordAccess
{
    /// <summary>
    /// Reads the subjects of the records the browser's record access session reaches now,
    /// each re-read as the session's own state read re-reads it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The subjects, in subject order, and none where the session reaches nothing; or
    /// <c>auth.session.expired</c> where the browser holds no record access session, or
    /// one past its lifetime or ended.
    /// </returns>
    /// <exception cref="InvalidOperationException">No browser request is being served.</exception>
    ValueTask<Result<IReadOnlyList<SubjectId>>> ReachedAsync(CancellationToken cancellationToken);
}
