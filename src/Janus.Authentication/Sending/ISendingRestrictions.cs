using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// The named restrictions as a send is judged against them, drawn on by an ask that
/// sends nothing of its own.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-002, AUTH-ABUSE-004 and CONV-DESIGN-003. An ask for a sign-in
/// link, an email code or a recovery that no message answers, because no account holds
/// the address or the account cannot be reached by it, counts against the restrictions
/// as the message would have, so a restriction refuses the next ask alike whether or not
/// an account holds the address. The draw stays inside this area: no other area asks
/// for a send that carries nothing. A send that carries a code or a link is named by
/// what answers it, so that the code presented right or the link pressed releases what
/// the send counted once against the restrictions marked <c>resend</c>; an ask that
/// sent nothing carries neither, and nothing a caller could observe releases its count
/// (AUTH-ABUSE-003).
/// </remarks>
internal interface ISendingRestrictions
{
    /// <summary>
    /// Judges one send that is not carried against every restriction that would govern
    /// it, inside the unit of work in progress and with its counters held, and, where
    /// none refuses it, counts it as the message would have been counted.
    /// </summary>
    /// <param name="message">The send the ask stands for, carrying no values.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the refusal the message would have met: <c>auth.restriction.exceeded</c>
    /// with <c>retryAt</c>, or the gateway floor for a text message.
    /// </returns>
    ValueTask<Result> DrawAsync(OutboundMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Names what an admitted send is answered by, inside the unit of work that
    /// undertook it: the code it carries or the link it carries.
    /// </summary>
    /// <param name="reference">The reference the send was admitted under.</param>
    /// <param name="answer">
    /// What names the code or the link and no other issued before or after it: the
    /// fingerprint of the link's token, or of the code with what holds it.
    /// </param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of naming it.</returns>
    ValueTask CarriesAsync(SendReference reference, [NeverLogged] byte[] answer, CancellationToken cancellationToken);

    /// <summary>
    /// Releases what every send answered by a code or a link counted once against the
    /// restrictions marked <c>resend</c>, inside the unit of work in which the code was
    /// presented right or the link pressed, as its last write. Nothing else is released,
    /// and a code or a link no counted send carries releases nothing.
    /// </summary>
    /// <param name="answer">What names the code or the link, as the send was named by it.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of releasing it.</returns>
    ValueTask AnsweredAsync([NeverLogged] byte[] answer, CancellationToken cancellationToken);
}
