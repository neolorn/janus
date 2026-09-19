using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Whether a recipient receives the data inside the jurisdiction or outside it, the
/// second being a transfer that needs a basis.
/// </summary>
/// <remarks>Implements PRIV-ROPA-002, INT-HOST-002, chapter 5 section 8.</remarks>
public enum RecipientLocation
{
    /// <summary>
    /// Inside the jurisdiction.
    /// </summary>
    [JsonStringEnumMemberName("inside")]
    Inside = 0,

    /// <summary>
    /// Outside it, which is a cross-border transfer.
    /// </summary>
    [JsonStringEnumMemberName("outside")]
    Outside = 1,
}
