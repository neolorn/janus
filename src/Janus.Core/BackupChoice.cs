using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a kind's backup setting adds to the primary to make the security-notice set.
/// </summary>
/// <remarks>Implements REG-IDENT-002 and chapter 10 section 5.17.</remarks>
public enum BackupChoice
{
    /// <summary>
    /// Every verified identifier of the kind. The default.
    /// </summary>
    [JsonStringEnumMemberName("all-verified")]
    AllVerified = 0,

    /// <summary>
    /// The primary alone.
    /// </summary>
    [JsonStringEnumMemberName("primary-only")]
    PrimaryOnly = 1,

    /// <summary>
    /// The primary and one named verified identifier.
    /// </summary>
    [JsonStringEnumMemberName("named")]
    Named = 2,
}
