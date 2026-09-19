using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Where a consent or objection record was made.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 5.21, PRIV-CONS-001, PRIV-RIGHT-001a, D-153.
/// </remarks>
public enum ConsentMechanism
{
    /// <summary>
    /// The terms step of registration.
    /// </summary>
    [JsonStringEnumMemberName("registration")]
    Registration = 0,

    /// <summary>
    /// The subject's own privacy pages.
    /// </summary>
    [JsonStringEnumMemberName("dashboard")]
    Dashboard = 1,

    /// <summary>
    /// The prompt raised when a document version changes materially.
    /// </summary>
    [JsonStringEnumMemberName("reconsent")]
    Reconsent = 2,

    /// <summary>
    /// Entered on the subject's behalf by an administrator.
    /// </summary>
    [JsonStringEnumMemberName("administrator")]
    Administrator = 3,
}
