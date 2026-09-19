using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What the age screen records where the deployment takes no adult affirmation. Finer
/// bands are host work where a host needs them.
/// </summary>
/// <remarks>Implements chapter 10 section 5.22, REG-PROF-002, D-153.</remarks>
public enum AgeGroup
{
    /// <summary>
    /// Below the age of majority the deployment applies.
    /// </summary>
    [JsonStringEnumMemberName("minor")]
    Minor = 0,

    /// <summary>
    /// At or above it.
    /// </summary>
    [JsonStringEnumMemberName("adult")]
    Adult = 1,
}
