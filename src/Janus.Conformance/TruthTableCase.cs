using System;
using System.Globalization;
using Janus.Core;

namespace Janus.Conformance;

/// <summary>
/// One row of the host's truth table: a scenario, a permission and the outcome the
/// host expects.
/// </summary>
/// <param name="Scenario">How the person stands to the record.</param>
/// <param name="Permission">What the person asks to do.</param>
/// <param name="Allowed">Whether the host expects it allowed.</param>
/// <exception cref="ArgumentException">
/// The scenario is a step-up scenario and the case states an outcome the scenario does
/// not have.
/// </exception>
/// <remarks>
/// Implements AUTHZ-TEST-001 and LIB-TEST-001 AC2. The table is the host's statement of
/// its policy, so changing the policy is changing a row first. A step-up scenario has
/// one outcome, allowed where the gate is met and refused in every other, so a case
/// stating the other is refused here and never reaches a run (chapter 10 section 5.30).
/// </remarks>
public sealed record TruthTableCase(TruthTableScenario Scenario, Permission Permission, bool Allowed)
{
    /// <summary>
    /// How the person stands to the record.
    /// </summary>
    public TruthTableScenario Scenario { get; } = Scenario;

    /// <summary>
    /// What the person asks to do.
    /// </summary>
    public Permission Permission { get; } = Permission;

    /// <summary>
    /// Whether the host expects it allowed.
    /// </summary>
    public bool Allowed { get; } = Stated(Scenario, Allowed);

    /// <summary>
    /// Whether the scenario is one judged from an assurance report.
    /// </summary>
    internal bool StepUp => Judged(Scenario);

    private static bool Judged(TruthTableScenario scenario) =>
        scenario is >= TruthTableScenario.StepUpMet and <= TruthTableScenario.StepUpProviderAbsent;

    // CONV-ERR-001: a row that contradicts its own scenario is a fault of the table,
    // raised where the row is made.
    private static bool Stated(TruthTableScenario scenario, bool allowed)
    {
        if (Judged(scenario) && allowed != (scenario == TruthTableScenario.StepUpMet))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A case of the scenario {scenario} states the action {(allowed ? "refused" : "allowed")} and no other outcome."),
                nameof(allowed));
        }

        return allowed;
    }
}
