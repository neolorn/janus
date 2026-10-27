using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The deliveries of host-side work that are outstanding, an erasure's, a takedown's, a
/// restriction change's, a consent change's or an objection change's, how far each has
/// got, and the manual completion of one a subscriber or purpose handler could not
/// finish.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-003a, IDN-LIFE-003b and chapter 09 section 8a
/// (D-203). Every operation is the <c>privacyrequest:manage</c> permission's; the
/// completion also asks the caller's session for <c>erasure:complete</c> and is
/// recorded.
/// </remarks>
public interface IErasures
{
    /// <summary>
    /// Every delivery of host-side work, of every kind, that is not complete, oldest
    /// first, read in one query.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The deliveries, or the refusal.</returns>
    ValueTask<Result<IReadOnlyList<ErasureProgress>>> ListAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// One delivery of any kind and how far each subscriber or purpose handler has got
    /// with it.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="erasure">The delivery's identifier, a takedown's <c>takedownId</c> among them.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The progress, or the refusal: <c>privacy.erasure.notfound</c> where no delivery
    /// is held under the identifier.
    /// </returns>
    ValueTask<Result<ErasureProgress>> ReadAsync(
        AccessContext context,
        ErasureId erasure,
        CancellationToken cancellationToken);

    /// <summary>
    /// Closes a delivery of any kind whose retries were spent, by the hand of an
    /// operator who has done the work the subscriber or purpose handler could not; an
    /// erasure's ledger line and its erasures row are closed with it.
    /// </summary>
    /// <param name="context">Who is completing it.</param>
    /// <param name="session">The session the step-up is judged on, after every other refusal.</param>
    /// <param name="erasure">The delivery's identifier, a takedown's <c>takedownId</c> among them.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>privacy.erasure.notfound</c> where no delivery is
    /// held under the identifier, and <c>privacy.erasure.notfailed</c> where its retries
    /// are not spent.
    /// </returns>
    ValueTask<Result> CompleteAsync(
        AccessContext context,
        SessionId session,
        ErasureId erasure,
        CancellationToken cancellationToken);
}
