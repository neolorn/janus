using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// Where what the provider did to a session is recorded. The area holds no audit trail
/// of its own, so what it has to record it hands out through this.
/// </summary>
/// <remarks>Implements AUTH-OIDC-001, AUTH-OIDC-003 and IDN-AUD-001.</remarks>
internal interface IOidcAudit
{
    /// <summary>
    /// Records that a refresh token was presented after it had already been used, and
    /// that everything derived from the session went with it.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="clientId">Which client presented it.</param>
    /// <param name="session">The session record the family stood on.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask ReusedAsync(
        SubjectId subject,
        string clientId,
        SessionId session,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a client was registered, or a registered one changed, from the
    /// server.
    /// </summary>
    /// <param name="principal">The command that registered it.</param>
    /// <param name="clientId">Which client.</param>
    /// <param name="kind">Which of the two kinds it is now.</param>
    /// <param name="changed">Whether the registry held it before.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RegisteredAsync(
        SystemPrincipal principal,
        string clientId,
        OidcClientKind kind,
        bool changed,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
