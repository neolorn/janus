using System.Text.Json.Serialization;

namespace Janus.Privacy.Outbox;

/// <summary>
/// Which fact a delivery carries, held on the row so the worker can raise the event
/// again without holding the event itself.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003a, PRIV-RIGHT-001a and CONV-ENUM-001. The names are the
/// library's own facts about identity and name no consumer and no consumer's domain. An
/// erasure, a restriction change and a takedown are delivered to the subject-event
/// subscribers; a consent change and an objection change to the purpose handlers naming
/// its purpose.
/// </remarks>
internal enum SubjectEventKind
{
    /// <summary>A subject was erased.</summary>
    [JsonStringEnumMemberName("erasure-requested")]
    ErasureRequested = 0,

    /// <summary>A subject's processing restriction was set or lifted.</summary>
    [JsonStringEnumMemberName("restriction-changed")]
    RestrictionChanged = 1,

    /// <summary>A subject's account was taken down.</summary>
    [JsonStringEnumMemberName("takedown-executed")]
    TakedownExecuted = 3,

    /// <summary>A subject's consent to a purpose was granted, withdrawn or superseded.</summary>
    [JsonStringEnumMemberName("consent-changed")]
    ConsentChanged = 4,

    /// <summary>A subject's objection to a purpose was recorded or withdrawn.</summary>
    [JsonStringEnumMemberName("objection-changed")]
    ObjectionChanged = 5,
}
