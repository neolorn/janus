using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What an account may do with its own sessions: see them, end one, and end them all.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTH-SESS-008, AUTH-SESS-009, AUTH-SESS-011 and
/// AUTH-SESS-013. Ending the
/// record a session stands on ends everything derived from it, so signing out is not
/// undone by moving to another application.
/// </remarks>
public interface ISessions
{
    /// <summary>
    /// The account's live sessions, with the one asking marked.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="current">The session the request arrived on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The sessions.</returns>
    ValueTask<Result<IReadOnlyList<SessionSummary>>> ListAsync(
        AccessContext context,
        SessionId current,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends one session and everything derived from it, leaving the others alone.
    /// Ending the session asking behaves as a sign-out.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">Which session.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the failure where the session is not the account's.</returns>
    ValueTask<Result> EndAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends every session of the account asking, which is signing out everywhere.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success.</returns>
    ValueTask<Result> EndEverywhereAsync(AccessContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every session of another account, which is what an offboarding and a
    /// suspension use. Distinct from ending every session in the deployment.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="subject">Whose sessions.</param>
    /// <param name="organization">The organization the caller is asking within.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or <c>authz.denied</c> where the caller may not.</returns>
    ValueTask<Result> RevokeAccountAsync(
        AccessContext context,
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends every session in the deployment, which is the emergency operation a
    /// suspected compromise calls for. Distinct from ending one account's.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">The organization the caller is asking within.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or <c>authz.denied</c> where the caller may not.</returns>
    ValueTask<Result> RevokeEveryAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken);
}
