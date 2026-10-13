using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The erasures whose host-side work is outstanding, how far each has got, and the
/// manual completion of an erasure, a takedown or a restriction delivery a subscriber
/// could not finish.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, IDN-LIFE-003a, IDN-LIFE-003b and chapter 09 section 8a. Every
/// operation is the <c>privacyrequest:manage</c> permission's; the completion also asks
/// the caller's session for <c>erasure:complete</c> and is recorded.
/// </remarks>
public interface IErasures
{
    /// <summary>
    /// Every erasure whose host-side work is outstanding, oldest first.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The erasures, or the refusal.</returns>
    ValueTask<Result<IReadOnlyList<ErasureProgress>>> ListAsync(
        AccessContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// One erasure and how far each subscriber has got with it.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="erasure">Which erasure.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The progress, or the refusal: <c>privacy.erasure.notfound</c> where no erasure is
    /// held under the identifier.
    /// </returns>
    ValueTask<Result<ErasureProgress>> ReadAsync(
        AccessContext context,
        ErasureId erasure,
        CancellationToken cancellationToken);

    /// <summary>
    /// Closes an erasure, a takedown or a restriction delivery whose retries were spent,
    /// by the hand of an operator who has done the work the subscriber could not; an
    /// erasure's ledger line and its erasures row are closed with it.
    /// </summary>
    /// <param name="context">Who is completing it.</param>
    /// <param name="session">The session the step-up is judged on, after every other refusal.</param>
    /// <param name="erasure">
    /// The delivery's identifier: an erasure's, a takedown's <c>takedownId</c>, or a
    /// restriction's.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>privacy.erasure.notfound</c> where no delivery of the
    /// three kinds is held under the identifier, and <c>privacy.erasure.notfailed</c>
    /// where its retries are not spent.
    /// </returns>
    ValueTask<Result> CompleteAsync(
        AccessContext context,
        SessionId session,
        ErasureId erasure,
        CancellationToken cancellationToken);
}
