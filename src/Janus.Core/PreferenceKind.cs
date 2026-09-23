using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The four types a host-declared preference may take. The library validates a value
/// against the type its key was declared with and never branches on the value itself.
/// </summary>
/// <remarks>Implements REG-PREF-001.</remarks>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "REG-PREF-001 names the four types string, boolean, integer and enum, and a host reads the declaration in those words.")]
public enum PreferenceKind
{
    /// <summary>
    /// Any text.
    /// </summary>
    [JsonStringEnumMemberName("string")]
    String = 0,

    /// <summary>
    /// True or false.
    /// </summary>
    [JsonStringEnumMemberName("boolean")]
    Boolean = 1,

    /// <summary>
    /// A whole number.
    /// </summary>
    [JsonStringEnumMemberName("integer")]
    Integer = 2,

    /// <summary>
    /// One of the values the declaration names.
    /// </summary>
    [JsonStringEnumMemberName("enum")]
    Enum = 3,
}
