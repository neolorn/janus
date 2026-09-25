using System.Collections.Generic;

namespace Janus.Conformance;

/// <summary>
/// What one part of the conformance suite found in the host's configuration.
/// </summary>
/// <param name="Findings">Each way the configuration does not conform, each distinct.</param>
/// <remarks>Implements LIB-TEST-001.</remarks>
public sealed record ConformanceReport(IReadOnlyList<ConformanceFinding> Findings)
{
    /// <summary>
    /// Whether nothing was found.
    /// </summary>
    public bool Conforms => Findings.Count == 0;
}
