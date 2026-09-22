using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a recipient of personal data is in law, which the records of processing
/// report beside its name.
/// </summary>
/// <remarks>Implements PRIV-ROPA-002 and CONV-ENUM-001.</remarks>
public enum RecipientCharacterisation
{
    /// <summary>
    /// Processes the data on the controller's instructions and for no purpose of its
    /// own, which is what an agreement is required for.
    /// </summary>
    [JsonStringEnumMemberName("processor")]
    Processor = 0,

    /// <summary>
    /// Receives the data and decides for itself what it does with it.
    /// </summary>
    [JsonStringEnumMemberName("recipient")]
    Recipient = 1,
}
