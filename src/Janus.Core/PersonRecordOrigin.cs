using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// How a person record came to be written.
/// </summary>
/// <remarks>
/// Implements REC-REC-001 and chapter 10 section 5.51. A record staff entered is reached
/// by no code and offered to no account, since no contact of it was proven by the person
/// (REC-COL-003).
/// </remarks>
public enum PersonRecordOrigin
{
    /// <summary>
    /// The person gave the data in a host's own screen (REC-COL-002).
    /// </summary>
    [JsonStringEnumMemberName("collection")]
    Collection = 0,

    /// <summary>
    /// A member of staff entered it for them (REC-COL-003).
    /// </summary>
    [JsonStringEnumMemberName("staff")]
    Staff = 1,
}
