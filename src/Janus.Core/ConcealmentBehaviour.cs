using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a denial on one record of a resource type discloses.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 5.11, AUTHZ-CONCEAL-001. Concealment is the default
/// because whatever a declaration omits is what most types will carry.
/// </remarks>
public enum ConcealmentBehaviour
{
    /// <summary>
    /// The denial is answered as a record that does not exist.
    /// </summary>
    [JsonStringEnumMemberName("conceal")]
    Conceal = 0,

    /// <summary>
    /// The denial says the record exists and is forbidden.
    /// </summary>
    [JsonStringEnumMemberName("disclose")]
    Disclose = 1,
}
