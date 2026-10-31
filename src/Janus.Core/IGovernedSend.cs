using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The one way an area undertakes a send: the message is judged against the named
/// restrictions in the caller's transaction, counted from its
/// admission and written to the library's outbox there, and carried after the commit.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004, CONV-LAYOUT-002 and CONV-DESIGN-002. The caller has begun
/// its unit of work: the send decides in it and begins none of its own, so a refusal
/// changes nothing and the caller goes on or rolls back as its operation requires. An
/// operation that rolls back sends nothing.
/// </remarks>
public interface IGovernedSend
{
    /// <summary>
    /// Undertakes one message inside the unit of work in progress.
    /// </summary>
    /// <param name="message">What is to be sent.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The reference the admitted message is carried under, or the refusal:
    /// <c>auth.restriction.exceeded</c> with <c>retryAt</c>. It answers the admission and
    /// never the delivery. A message owed in every declared language is one message
    /// carrying them all, under one reference, which a text counts as many times as it
    /// carries languages (INT-SMS-006, D-225).
    /// </returns>
    ValueTask<Result<SendReference>> UndertakeAsync(
        OutboundMessage message,
        CancellationToken cancellationToken);
}
