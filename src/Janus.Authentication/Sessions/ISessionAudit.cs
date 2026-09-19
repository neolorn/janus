using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Where an authentication is recorded. The factors are named here and nowhere else:
/// the session records what was reached, the audit trail records what reached it.
/// </summary>
/// <remarks>Implements AUTH-SESS-002 and IDN-AUD-001.</remarks>
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
}
