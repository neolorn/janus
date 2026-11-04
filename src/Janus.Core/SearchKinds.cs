using System;
using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The kinds of subject a staff search asks for, none meaning both.
/// </summary>
/// <remarks>Implements IDN-SRCH-002, LIB-API-005 and chapter 10 section 5.53 (D-256).</remarks>
[Flags]
public enum SearchKinds
{
    /// <summary>
    /// No kind named, which asks for both.
    /// </summary>
    None = 0,

    /// <summary>
    /// Accounts, read under <c>contact:read</c>.
    /// </summary>
    [JsonStringEnumMemberName("account")]
    Accounts = 1,

    /// <summary>
    /// Person records, read under <c>record:read</c>.
    /// </summary>
    [JsonStringEnumMemberName("record")]
    Records = 2,
}
