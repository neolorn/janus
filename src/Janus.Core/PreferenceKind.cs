using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The four types a host-declared preference may take. The library validates a value
/// against the type its key was declared with and never branches on the value itself.
/// </summary>
/// <remarks>Implements REG-PREF-001.</remarks>
public enum PreferenceKind
{
    /// <summary>
    /// Any text.
    /// </summary>
    [JsonStringEnumMemberName("string")]
    Text = 0,

    /// <summary>
    /// True or false.
    /// </summary>
    [JsonStringEnumMemberName("boolean")]
    Flag = 1,

    /// <summary>
    /// A whole number.
    /// </summary>
    [JsonStringEnumMemberName("integer")]
    Number = 2,

    /// <summary>
    /// One of the values the declaration names.
    /// </summary>
    [JsonStringEnumMemberName("enum")]
    Choice = 3,
}
