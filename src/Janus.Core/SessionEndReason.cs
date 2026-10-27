using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Why a session ended before its expiry.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-010 and chapter 10 sections 5.39 and 5b (D-218), whose
/// <c>SessionEnded</c> row names the five.
/// </remarks>
public enum SessionEndReason
{
    /// <summary>
    /// The person signed out of it.
    /// </summary>
    [JsonStringEnumMemberName("signedout")]
    SignedOut = 0,

    /// <summary>
    /// The person ended it from another session or everywhere, or a change to their
    /// credentials or identifiers ended their other sessions.
    /// </summary>
    [JsonStringEnumMemberName("ended")]
    Ended = 1,

    /// <summary>
    /// An administrator revoked the account's sessions or every session.
    /// </summary>
    [JsonStringEnumMemberName("revoked")]
    Revoked = 2,

    /// <summary>
    /// The account was suspended, deactivated, deleted, taken down or erased.
    /// </summary>
    [JsonStringEnumMemberName("account")]
    Account = 3,

    /// <summary>
    /// The deletion of its organization was requested (IDN-ORG-003).
    /// </summary>
    [JsonStringEnumMemberName("organization")]
    Organization = 4,
}
