using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Where a sign-in stands after a factor was presented.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-001 and AUTH-FACT-016. The status reports the state of the
/// attempt and never which factor produced it (`09` section 3).
/// </remarks>
public enum SignInStatus
{
    /// <summary>The session exists and the browser carries it.</summary>
    [JsonStringEnumMemberName("complete")]
    Complete = 0,

    /// <summary>More is needed before the policy's assurance is reached.</summary>
    [JsonStringEnumMemberName("factorRequired")]
    FactorRequired = 1,

    /// <summary>The new-device check holds it until the emailed code is entered.</summary>
    [JsonStringEnumMemberName("deviceVerificationRequired")]
    DeviceVerificationRequired = 2,
}
