using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The one state a person record is in at any time.
/// </summary>
/// <remarks>
/// Implements REC-REC-002 and chapter 10 section 5.51. A record has no suspended state,
/// since it signs nothing in: a takedown takes it straight into its deletion window.
/// </remarks>
public enum PersonRecordState
{
    /// <summary>
    /// Normal.
    /// </summary>
    [JsonStringEnumMemberName("active")]
    Active = 0,

    /// <summary>
    /// Processing restricted at the person's request. The record is read and its rights
    /// exercised; its own fields are not changed.
    /// </summary>
    [JsonStringEnumMemberName("restricted")]
    Restricted = 1,

    /// <summary>
    /// Deletion requested and its grace window running.
    /// </summary>
    [JsonStringEnumMemberName("deleting")]
    Deleting = 2,

    /// <summary>
    /// The personal data is key-destroyed and the row is kept.
    /// </summary>
    [JsonStringEnumMemberName("deleted")]
    Deleted = 3,
}
