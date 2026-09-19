using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Which channel a send goes out on.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-004, LIB-HOST-001, D-153.</remarks>
public enum SendKind
{
    /// <summary>
    /// An email address.
    /// </summary>
    [JsonStringEnumMemberName("email")]
    Email = 0,

    /// <summary>
    /// A telephone number.
    /// </summary>
    [JsonStringEnumMemberName("sms")]
    Sms = 1,
}
