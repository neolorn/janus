using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Where an authentication is recorded. The factors are named here and nowhere else:
/// the session records what was reached, the audit trail records what reached it, and
/// what was refused on the way.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-002, IDN-AUD-001 and CONV-LOG-005. A refusal is written to the
/// trail, which no log level governs, and never with the identifier as it was typed or
/// anything that was presented.
/// </remarks>
internal interface ISessionAudit
{
    /// <summary>
    /// Records that a combination was presented on a session.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="subject">Whose.</param>
    /// <param name="presented">What was presented.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask PresentedAsync(
        SessionId session,
        SubjectId subject,
        IReadOnlyCollection<Factor> presented,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a factor presented to authenticate was refused. No actor was
    /// established, so the record names none.
    /// </summary>
    /// <param name="subject">
    /// The account the attempt was made against, or nothing where the library resolved
    /// none.
    /// </param>
    /// <param name="presented">Which factor was refused.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask FailedAsync(
        SubjectId? subject,
        Factor presented,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a factor presented to step a live session up was refused.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="subject">Whose.</param>
    /// <param name="presented">Which factor was refused.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask StepUpFailedAsync(
        SessionId session,
        SubjectId subject,
        Factor presented,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
