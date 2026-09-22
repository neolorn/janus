using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a data subject request asks for.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-001 and chapter 10 section 5.12c. Rectification of editable
/// data is account editing and raises no request; only what the subject cannot edit
/// themselves reaches the queue.
/// </remarks>
public enum PrivacyRequestType
{
    /// <summary>
    /// The subject asks that processing be suspended pending a dispute.
    /// </summary>
    [JsonStringEnumMemberName("restriction")]
    Restriction = 0,

    /// <summary>
    /// The subject asks that data they cannot edit themselves be corrected.
    /// </summary>
    [JsonStringEnumMemberName("rectification")]
    Rectification = 1,

    /// <summary>
    /// The subject asks to be erased, out of band, through a human.
    /// </summary>
    [JsonStringEnumMemberName("erasure")]
    Erasure = 2,
}
