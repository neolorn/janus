using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Which retention an audit record falls under, which is the only thing about a record
/// the library branches on.
/// </summary>
/// <remarks>
/// Implements PRIV-RET-001, PRIV-RET-002 and chapter 10 section 4.7, whose two audit
/// retention keys are these two categories. A record is written to the partition of its
/// category, so a partition holds records of one retention and is dropped whole.
/// </remarks>
public enum AuditCategory
{
    /// <summary>
    /// Security events, permission changes and financial actions, kept for
    /// <c>retention.audit.security</c>.
    /// </summary>
    [JsonStringEnumMemberName("security")]
    Security = 0,

    /// <summary>
    /// Routine access logging, kept for <c>retention.audit.routine</c>.
    /// </summary>
    [JsonStringEnumMemberName("routine")]
    Routine = 1,
}
