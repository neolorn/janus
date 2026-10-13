using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The sealed emergency credential as a host holds it: generating it, a first issue or
/// a replacement, and reading whether one stands.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, OPS-BOOT-001 AC3 and OPS-BOOT-004. Presenting the credential
/// opens the browser's session, which is the boundary's own work, so it is not here.
/// Each operation checks what the route checks, so a host calling in process meets the
/// same refusals.
/// </remarks>
public interface IBreakGlass
{
    /// <summary>
    /// Generates the credential, a first issue or a replacement that invalidates the one
    /// before it, and answers it the one time it is shown.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request arrived on, whose step-up is judged.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The credential, or <c>authz.denied</c> without <c>system:administer</c>, or
    /// <c>auth.stepup.required</c> where the session has not stepped up.
    /// </returns>
    ValueTask<Result<GeneratedBreakGlass>> GenerateAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a credential stands, and since when, with nothing of the credential.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// When the standing credential was generated, or nothing where none stands; or
    /// <c>authz.denied</c> without <c>system:administer</c>.
    /// </returns>
    ValueTask<Result<DateTimeOffset?>> StandingAsync(
        AccessContext context,
        CancellationToken cancellationToken);
}
