using Janus.Core;

namespace Janus.Conformance;

/// <summary>
/// One way the host's configuration does not conform.
/// </summary>
/// <param name="Check">Which part of the suite found it.</param>
/// <param name="Failure">What was found, as a code and the structured data naming it.</param>
/// <remarks>
/// Implements LIB-TEST-001 and LIB-API-003. A finding is a code with its data, as
/// every failure crossing the boundary is, so the host's test states it in its own
/// words.
/// </remarks>
public sealed record ConformanceFinding(ConformanceCheck Check, Error Failure);
