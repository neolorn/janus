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
/// whatever the gate costs: the lowest level, the oldest instant, the latest instant.
/// </remarks>
internal sealed class ScenarioAssurance(TruthTableScenario scenario, DateTimeOffset judgedAt) : IAssuranceProvider
{
    /// <inheritdoc/>
    public ValueTask<Result<AttainedAssurance>> AttainedAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        var met = new AttainedAssurance(
            AssuranceLevel.Aal3,
            PhishingResistant: true,
            judgedAt,
            AssuranceLevel.Aal3);

        return ValueTask.FromResult(scenario switch
        {
            TruthTableScenario.StepUpMet => Result.Success(met),
            TruthTableScenario.StepUpLevelUnmet => Result.Success(met with { Level = AssuranceLevel.Delegated }),
            TruthTableScenario.StepUpPhishingResistanceUnmet => Result.Success(met with { PhishingResistant = false }),
            TruthTableScenario.StepUpAgeUnmet => Result.Success(met with { AttainedAt = DateTimeOffset.MinValue }),
            TruthTableScenario.StepUpInstantFuture => Result.Success(met with { AttainedAt = DateTimeOffset.MaxValue }),
            TruthTableScenario.StepUpProviderFailed => Result.Failure<AttainedAssurance>(Error.From(ErrorCodes.SystemFault)),
            _ => throw new InvalidOperationException("The scenario is judged from no report."),
        });
    }
}
