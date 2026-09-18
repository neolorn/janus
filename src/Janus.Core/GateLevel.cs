using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The level a step-up gate demands: a stated tier, or whatever tier the account can
/// reach with the factors it holds.
/// </summary>
/// <remarks>Implements chapter 10 section 4.1a, AUTH-STEP-002, AUTH-STEP-002a.</remarks>
public enum GateLevel
{
    /// <summary>
    /// The account's reachable assurance, with a floor of <see cref="Aal1"/>, so a
    /// person who cannot reach a tier is offered enrolment rather than refused.
    /// </summary>
    [JsonStringEnumMemberName("reachable")]
    Reachable = 0,

    /// <summary>
    /// One factor.
    /// </summary>
    [JsonStringEnumMemberName("aal1")]
    Aal1 = 1,

    /// <summary>
    /// Two factors, or one that stands alone.
    /// </summary>
    [JsonStringEnumMemberName("aal2")]
    Aal2 = 2,
}
