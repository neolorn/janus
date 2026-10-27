using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The kind of a delivery of host-side work: the work of an erasure, of a takedown's
/// trigger and of a restriction change, delivered to the subject-event subscribers, and
/// the work of a consent change and of an objection change, delivered to the purpose
/// handlers naming its purpose.
/// </summary>
/// <remarks>Implements IDN-LIFE-003a and chapter 10 section 5.50.</remarks>
public enum DeliveryKind
{
    /// <summary>The work of an erasure (<c>ErasureRequested</c>).</summary>
    [JsonStringEnumMemberName("erasure")]
    Erasure = 0,

    /// <summary>The work of a takedown's trigger (<c>TakedownExecuted</c>).</summary>
    [JsonStringEnumMemberName("takedown")]
    Takedown = 1,

    /// <summary>The work of a restriction change (<c>RestrictionChanged</c>).</summary>
    [JsonStringEnumMemberName("restriction")]
    Restriction = 2,

    /// <summary>The work of a consent change (<c>ConsentChanged</c>).</summary>
    [JsonStringEnumMemberName("consent")]
    Consent = 3,

    /// <summary>The work of an objection change (<c>ObjectionChanged</c>).</summary>
    [JsonStringEnumMemberName("objection")]
    Objection = 4,
}
