using System.Text.Json.Serialization;

namespace Janus.Core.Configuration;

/// <summary>
/// Whether an attribute a registration may collect is off, offered or demanded.
/// </summary>
/// <remarks>Implements chapter 10 section 4.6, REG-PROF-001, REG-IDENT-001.</remarks>
public enum AttributeRequirement
{
    /// <summary>
    /// Not collected, and no request accepts it.
    /// </summary>
    [JsonStringEnumMemberName("off")]
    Off = 0,

    /// <summary>
    /// Collected when the person supplies it.
    /// </summary>
    [JsonStringEnumMemberName("optional")]
    Optional = 1,

    /// <summary>
    /// Collected before the registration completes.
    /// </summary>
    [JsonStringEnumMemberName("required")]
    Required = 2,
}
