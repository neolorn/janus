using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// Where what a deployment knows about a number is written down before a restricted
/// factor goes to it.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-002b and IDN-AUD-001. The number itself is never among it:
/// the record says which entry was about to be sent and what was known, which is what
/// an operator reads to decide whether the channel is still worth offering.
/// </remarks>
internal interface IPhoneSignalAudit
{
    /// <summary>
    /// Records what was known about the number a restricted factor was about to go to.
    /// </summary>
    /// <param name="factor">The entry the message amounts to.</param>
    /// <param name="signal">
    /// What the provider answered, and absent where the deployment registered none.
    /// </param>
    /// <param name="subject">Whose account the number belongs to, where it belongs to one.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask ConsideredAsync(
        Factor factor,
        PhoneSignal? signal,
        SubjectId? subject,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
