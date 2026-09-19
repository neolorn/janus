using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// Where an event about one credential is recorded. The area holds no audit trail of
/// its own, so what it has to record it hands out through this.
/// </summary>
/// <remarks>Implements AUTH-FACT-014, IDN-AUD-001 and CONV-LAYOUT-001.</remarks>
internal interface ICredentialAudit
{
    /// <summary>
    /// Records that something happened to a credential.
    /// </summary>
    /// <param name="action">What happened.</param>
    /// <param name="subject">Whose credential.</param>
    /// <param name="credential">Which credential.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        SubjectId subject,
        AuthenticatorId credential,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
