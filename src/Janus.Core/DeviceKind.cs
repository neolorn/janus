using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a browser the account knows is known for: one whose trust skips a second
/// factor, or one the new-device check has already seen.
/// </summary>
/// <remarks>
/// Implements chapter 9 section 6, AUTH-FACT-015 and AUTH-FACT-016. The two are
/// separate records: a remembered browser skips nothing.
/// </remarks>
public enum DeviceKind
{
    /// <summary>
    /// A browser whose trust stands in for the second factor of a sign-in, and for
    /// nothing else.
    /// </summary>
    [JsonStringEnumMemberName("trusted")]
    Trusted = 0,

    /// <summary>
    /// A browser the new-device check has seen, which is not held for a code again
    /// while it is remembered.
    /// </summary>
    [JsonStringEnumMemberName("remembered")]
    Remembered = 1,
}
