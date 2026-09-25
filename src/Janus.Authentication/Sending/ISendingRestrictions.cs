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
/// an account holds the address.
/// </remarks>
internal interface ISendingRestrictions
{
    /// <summary>
    /// Judges one send that is not carried against every restriction that would govern
    /// it and, where none refuses it, counts it as the message would have been counted.
    /// </summary>
    /// <param name="request">The send the ask stands for, carrying no values.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the refusal the message would have met: <c>auth.restriction.exceeded</c>
    /// with <c>retryAt</c>, or the gateway floor for a text message.
    /// </returns>
    ValueTask<Result> DrawAsync(SendRequest request, CancellationToken cancellationToken);
}
