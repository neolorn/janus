using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.BreakGlass;

/// <summary>
/// Where what happened to the break-glass credential is written down.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-002, OPS-BOOT-004 and IDN-AUD-001. The entry names the issue
/// and never holds anything of the code.
/// </remarks>
internal interface IBreakGlassAudit
{
    /// <summary>
    /// Records that the credential was generated.
    /// </summary>
    /// <param name="acting">Who generated it: a system administrator or the emergency account.</param>
    /// <param name="credential">The issue generated.</param>
    /// <param name="replaced">The issue it replaced, where one stood.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask GeneratedAsync(
        SubjectId acting,
        BreakGlassCredentialId credential,
        BreakGlassCredentialId? replaced,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that the credential was used, with the emergency account as acting and
    /// effective identity.
    /// </summary>
    /// <param name="emergency">The emergency account.</param>
    /// <param name="credential">The issue used.</param>
    /// <param name="session">The session it opened.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask UsedAsync(
        SubjectId emergency,
        BreakGlassCredentialId credential,
        SessionId session,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
