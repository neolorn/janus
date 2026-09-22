using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests.Exports;

/// <summary>
/// The step-up gate, answering as a test has set it and recording what it was asked.
/// </summary>
internal sealed class StepUpGateInMemory : IStepUpGate
{
    private readonly List<(SubjectId Subject, SessionId Session, StepUpAction Action)> _asked = [];

    /// <summary>
    /// What the gate was asked, in order.
    /// </summary>
    public IReadOnlyList<(SubjectId Subject, SessionId Session, StepUpAction Action)> Asked => _asked;

    /// <summary>
    /// What the gate answers, which is success until a test closes it.
    /// </summary>
    public Error? Closed { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result> RequireAsync(
        SubjectId subject,
        SessionId session,
        StepUpAction action,
        CancellationToken cancellationToken)
    {
        _asked.Add((subject, session, action));

        return ValueTask.FromResult(
            Closed is Error closed ? Result.Failure(closed) : Result.Success());
    }
}
