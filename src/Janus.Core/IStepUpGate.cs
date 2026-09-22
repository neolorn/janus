using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The one place a session is asked to prove it is still the account. A step-up is
/// resolved against the account's own policy, so an area that gates an operation
/// asks here rather than reading factors of its own.
/// </summary>
/// <remarks>
/// Implements AUTH-STEP-001, AUTH-STEP-002a, LIB-SEAM-001 and CONV-LAYOUT-001. The
/// twin of <see cref="IAccessGate"/>: that one answers whether the principal may,
/// this one whether the session has proved recently enough that it is them.
/// </remarks>
public interface IStepUpGate
{
    /// <summary>
    /// Whether the session has proved what an action costs.
    /// </summary>
    /// <param name="subject">Whose account the action is on.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="action">Which action.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success where it has, and <c>auth.stepup.required</c> where it has not. What
    /// the person could present instead is the step-up endpoint's business.
    /// </returns>
    ValueTask<Result> RequireAsync(
        SubjectId subject,
        SessionId session,
        StepUpAction action,
        CancellationToken cancellationToken);
}
