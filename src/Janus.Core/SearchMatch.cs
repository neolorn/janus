using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The field of a subject a staff search matched first, in the order name, email,
/// phone.
/// </summary>
/// <remarks>Implements IDN-SRCH-001, LIB-API-005 and chapter 10 section 5.53 (D-256).</remarks>
public enum SearchMatch
{
    /// <summary>
    /// The name.
    /// </summary>
    [JsonStringEnumMemberName("name")]
    Name = 0,

    /// <summary>
    /// An email.
    /// </summary>
    [JsonStringEnumMemberName("email")]
    Email = 1,

    /// <summary>
    /// A phone.
    /// </summary>
    [JsonStringEnumMemberName("phone")]
    Phone = 2,
}
