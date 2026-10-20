using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The change the event a delivery of host-side work carries announces: a consent's,
/// an objection's or a restriction's. An erasure and a takedown carry none.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003a and chapter 10 section 5.50. A consent's change is spelled
/// as <see cref="ConsentChange"/> spells it; the events themselves are unchanged.
/// </remarks>
public enum DeliveryChange
{
    /// <summary>A consent was granted.</summary>
    [JsonStringEnumMemberName("granted")]
    Granted = 0,

    /// <summary>A consent or an objection was withdrawn.</summary>
    [JsonStringEnumMemberName("withdrawn")]
    Withdrawn = 1,

    /// <summary>A consent was superseded by a material revision of its document.</summary>
    [JsonStringEnumMemberName("superseded")]
    Superseded = 2,

    /// <summary>An objection was recorded.</summary>
    [JsonStringEnumMemberName("recorded")]
    Recorded = 3,

    /// <summary>A restriction was set.</summary>
    [JsonStringEnumMemberName("set")]
    Set = 4,

    /// <summary>A restriction was lifted.</summary>
    [JsonStringEnumMemberName("lifted")]
    Lifted = 5,
}
