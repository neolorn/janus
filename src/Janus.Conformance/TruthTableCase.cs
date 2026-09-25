using Janus.Core;

namespace Janus.Conformance;

/// <summary>
/// One row of the host's truth table: a scenario, a permission and the outcome the
/// host expects.
/// </summary>
/// <param name="Scenario">How the person stands to the record.</param>
/// <param name="Permission">What the person asks to do.</param>
/// <param name="Allowed">Whether the host expects it allowed.</param>
/// <remarks>
/// Implements AUTHZ-TEST-001. The table is the host's statement of its policy, so
/// changing the policy is changing a row first.
/// </remarks>
public sealed record TruthTableCase(TruthTableScenario Scenario, Permission Permission, bool Allowed);
