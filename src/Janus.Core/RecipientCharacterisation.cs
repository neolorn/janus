using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a recipient of personal data is in law, which the generated records of
/// processing state for each of them.
/// </summary>
/// <remarks>Implements PRIV-ROPA-002, INT-GEN-004, chapter 5 section 8.</remarks>
public enum RecipientCharacterisation
{
    /// <summary>
    /// Processes on the controller's instructions and for no purpose of its own.
    /// </summary>
    [JsonStringEnumMemberName("processor")]
    Processor = 0,

    /// <summary>
    /// Receives data and decides for itself what it does with it.
    /// </summary>
    [JsonStringEnumMemberName("recipient")]
    Recipient = 1,
}
