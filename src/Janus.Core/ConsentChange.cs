using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What happened to a consent.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-007, PRIV-CONS-008 and chapter 10 section 5b, whose
/// <c>ConsentChanged</c> row names the three.
/// </remarks>
public enum ConsentChange
{
    /// <summary>
    /// The subject gave it.
    /// </summary>
    [JsonStringEnumMemberName("granted")]
    Granted = 0,

    /// <summary>
    /// The subject took it back, and the handlers erase what was held solely for the
    /// purpose.
    /// </summary>
    [JsonStringEnumMemberName("withdrawn")]
    Withdrawn = 1,

    /// <summary>
    /// A material revision of the notice it was given against ended it, and the
    /// subject is asked again.
    /// </summary>
    [JsonStringEnumMemberName("superseded")]
    Superseded = 2,
}
