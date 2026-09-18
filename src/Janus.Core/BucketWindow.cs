using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// How a bucket's interval is counted.
/// </summary>
/// <remarks>Implements chapter 10 section 5.16, AUTH-ABUSE-004.</remarks>
public enum BucketWindow
{
    /// <summary>
    /// Counts the sends in the interval ending now.
    /// </summary>
    [JsonStringEnumMemberName("sliding")]
    Sliding = 0,

    /// <summary>
    /// Resets at the interval boundary.
    /// </summary>
    [JsonStringEnumMemberName("fixed")]
    Fixed = 1,
}
