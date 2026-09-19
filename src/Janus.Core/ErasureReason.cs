using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Why an erasure ran.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003b and chapter 10 section 5.12a. The erasures table and the
/// off-host erasure ledger use these spellings and no others.
/// </remarks>
public enum ErasureReason
{
    /// <summary>
    /// The subject exercised the erasure right.
    /// </summary>
    [JsonStringEnumMemberName("erasure-request")]
    ErasureRequest = 0,

    /// <summary>
    /// A takedown's grace window elapsed without reversal.
    /// </summary>
    [JsonStringEnumMemberName("minor-takedown")]
    MinorTakedown = 1,

    /// <summary>
    /// An organization's deletion grace window elapsed.
    /// </summary>
    [JsonStringEnumMemberName("organization-erasure")]
    OrganizationErasure = 2,
}
