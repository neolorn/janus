using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Conformance;

/// <summary>
/// The suite's own assurance provider for one step-up scenario: it gives the report the
/// scenario names, or fails to give one.
/// </summary>
/// <param name="scenario">The step-up scenario the report is for.</param>
/// <param name="judgedAt">The deployment's instant before the case is asked.</param>
/// <remarks>
/// Implements LIB-TEST-001 AC2 and LIB-HOST-004 (chapter 10 section 5.30). Each report
/// differs from the one that meets every gate in the one value its scenario names, and
/// that value is the furthest the type holds, so the outcome follows from the report
/// whatever the gate costs: no level reached, phishing resistance never reached, the
/// oldest instant, the latest instant.
/// </remarks>
internal sealed class ScenarioAssurance(TruthTableScenario scenario, DateTimeOffset judgedAt) : IAssuranceProvider
{
    /// <inheritdoc/>
    public ValueTask<Result<AttainedAssurance>> AttainedAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        AttainedAssurance met = Reached(judgedAt);

        return ValueTask.FromResult(scenario switch
        {
            TruthTableScenario.StepUpMet => Result.Success(met),
            TruthTableScenario.StepUpLevelUnmet => Result.Success(met with { Aal1At = null, Aal2At = null, Aal3At = null }),
            TruthTableScenario.StepUpPhishingResistanceUnmet => Result.Success(met with { PhishingResistantAt = null }),
            TruthTableScenario.StepUpAgeUnmet => Result.Success(Reached(DateTimeOffset.MinValue)),
            TruthTableScenario.StepUpInstantFuture => Result.Success(Reached(DateTimeOffset.MaxValue)),
            TruthTableScenario.StepUpProviderFailed => Result.Failure<AttainedAssurance>(Error.From(ErrorCodes.SystemFault)),
            _ => throw new InvalidOperationException("The scenario is judged from no report."),
        });
    }

    // A report that reached every level and phishing resistance at one instant.
    private static AttainedAssurance Reached(DateTimeOffset at) =>
        new(Aal1At: at, Aal2At: at, Aal3At: at, PhishingResistantAt: at, AssuranceLevel.Aal3);
}
