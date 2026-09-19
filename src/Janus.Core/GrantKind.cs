using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Where a grant came from: a row someone wrote, a fact in the host's own data, or a
/// fact precomputed into a row.
/// </summary>
/// <remarks>Implements chapter 10 section 5.6, AUTHZ-GRANT-001, AUTHZ-DERIVE-005.</remarks>
public enum GrantKind
{
    /// <summary>
    /// A row someone wrote.
    /// </summary>
    [JsonStringEnumMemberName("stored")]
    Stored = 0,

    /// <summary>
    /// Computed from a declared relationship in the host's own data.
    /// </summary>
    [JsonStringEnumMemberName("derived")]
    Derived = 1,

    /// <summary>
    /// A derivation precomputed into a row, and never mistaken for one someone wrote.
    /// </summary>
    [JsonStringEnumMemberName("materialised")]
    Materialised = 2,
}
