using System.Text.Json.Serialization;

namespace Janus.Conformance;

/// <summary>
/// Which part of the conformance suite a finding came from.
/// </summary>
/// <remarks>Implements LIB-TEST-001.</remarks>
public enum ConformanceCheck
{
    /// <summary>
    /// Every entity the host queries has a registered policy (LIB-TEST-001 AC1).
    /// </summary>
    [JsonStringEnumMemberName("policies")]
    Policies = 0,

    /// <summary>
    /// The host's truth table decides the same way through the single check and the
    /// list filter (LIB-TEST-001 AC2).
    /// </summary>
    [JsonStringEnumMemberName("truth-table")]
    TruthTable = 1,

    /// <summary>
    /// The model declaration holds together (LIB-TEST-001 AC3).
    /// </summary>
    [JsonStringEnumMemberName("declaration")]
    Declaration = 2,

    /// <summary>
    /// The provider refuses each form AUTH-OIDC-006 retires.
    /// </summary>
    [JsonStringEnumMemberName("provider")]
    Provider = 3,
}
