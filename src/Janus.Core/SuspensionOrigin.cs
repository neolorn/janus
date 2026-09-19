using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Who suspended an account, which decides how it is reactivated.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-013 and chapter 10 section 5.12b. A self-suspended account is
/// reactivated by its owner through the link in the deactivation notice or through
/// ordinary recovery; an administratively suspended one only by an administrator.
/// </remarks>
public enum SuspensionOrigin
{
    /// <summary>
    /// The account's owner deactivated it.
    /// </summary>
    [JsonStringEnumMemberName("self")]
    Self = 0,

    /// <summary>
    /// An administrator suspended it.
    /// </summary>
    [JsonStringEnumMemberName("administrator")]
    Administrator = 1,
}
