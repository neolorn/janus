using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a subject a read answers is: an account, or a person record.
/// </summary>
/// <remarks>Implements IDN-READ-003 and REC-ADM-001 (D-221).</remarks>
public enum SubjectKind
{
    /// <summary>
    /// An account.
    /// </summary>
    [JsonStringEnumMemberName("account")]
    Account = 0,

    /// <summary>
    /// A person record.
    /// </summary>
    [JsonStringEnumMemberName("record")]
    Record = 1,
}
