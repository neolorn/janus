using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// The step-up seam, over the guard every gated operation of this area already asks.
/// </summary>
/// <param name="guard">What judges the session against the account's policy.</param>
/// <remarks>
/// Implements LIB-SEAM-001, AUTH-STEP-001 and CONV-LAYOUT-001. An area that gates an
/// operation of its own reaches the one judgement through this rather than reading
/// factors it does not own.
/// </remarks>
internal sealed class StepUpGate(StepUpGuard guard) : IStepUpGate
{
    /// <inheritdoc/>
    public async ValueTask<Result> RequireAsync(
        SubjectId subject,
        SessionId session,
        StepUpAction action,
        CancellationToken cancellationToken) =>
        await guard.PassedAsync(subject, session, action, cancellationToken).ConfigureAwait(false)
            is Error closed
            ? Result.Failure(closed)
            : Result.Success();
}
