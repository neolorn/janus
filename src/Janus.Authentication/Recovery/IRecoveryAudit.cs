using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Recovery;

/// <summary>
/// Where an approval is recorded. The area holds no audit trail of its own, so what
/// it has to record it hands out through this.
/// </summary>
/// <remarks>Implements AUTH-RECOV-002, AUTH-RECOV-003 and IDN-AUD-001.</remarks>
internal interface IRecoveryAudit
{
    /// <summary>
    /// Records that one approver approved a re-enrolment, with the reason they wrote
    /// and the channel they confirmed the person on.
    /// </summary>
    /// <param name="approver">Who approved.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="reason">The written reason.</param>
    /// <param name="channel">Which kind of channel carried the confirmation.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask ApprovedAsync(
        SubjectId approver,
        SubjectId subject,
        string reason,
        IdentifierKind channel,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
