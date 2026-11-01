using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The grammatical gender a person is addressed in, as CLDR's
/// <c>grammaticalFeatures</c> spells the two that address a person. It records how the
/// person is addressed, not a legal sex, and nothing derives it from another field.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-007 and REG-PROF-001 (D-236). A message selects on it as
/// MessageFormat 2 selects on any string, an absent value taking the message's default
/// form.
/// </remarks>
public enum GrammaticalGender
{
    /// <summary>
    /// The masculine form.
    /// </summary>
    [JsonStringEnumMemberName("masculine")]
    Masculine = 0,

    /// <summary>
    /// The feminine form.
    /// </summary>
    [JsonStringEnumMemberName("feminine")]
    Feminine = 1,
}
