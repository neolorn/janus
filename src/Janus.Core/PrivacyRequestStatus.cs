using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Where a data subject request stands.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-002 and chapter 10 section 5.12c. The two lapse statuses are
/// what the six working days produce when nobody decided: a restriction is granted,
/// an erasure is deemed refused.
/// </remarks>
public enum PrivacyRequestStatus
{
    /// <summary>
    /// Undecided, and the clock is running.
    /// </summary>
    [JsonStringEnumMemberName("open")]
    Open = 0,

    /// <summary>
    /// A human decided to do what was asked.
    /// </summary>
    [JsonStringEnumMemberName("fulfilled")]
    Fulfilled = 1,

    /// <summary>
    /// A human decided not to, and recorded the reason.
    /// </summary>
    [JsonStringEnumMemberName("refused")]
    Refused = 2,

    /// <summary>
    /// The deadline passed undecided on a restriction, which is granted.
    /// </summary>
    [JsonStringEnumMemberName("granted-by-lapse")]
    GrantedByLapse = 3,

    /// <summary>
    /// The deadline passed undecided on an erasure, which the statute deems a
    /// rejection.
    /// </summary>
    [JsonStringEnumMemberName("deemed-refused-by-lapse")]
    DeemedRefusedByLapse = 4,
}
