using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Why a session ended before its expiry.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-010 and chapter 10 sections 5.39 and 5b (D-218, D-219), whose
/// <c>SessionEnded</c> row names the six.
/// </remarks>
public enum SessionEndReason
{
    /// <summary>
    /// The person signed out, which ends every session of the sign-in.
    /// </summary>
    [JsonStringEnumMemberName("signedout")]
    SignedOut = 0,

    /// <summary>
    /// The person ended it through their session list, whichever session it names, or a
    /// change to their credentials or identifiers ended their other sessions.
    /// </summary>
    [JsonStringEnumMemberName("ended")]
    Ended = 1,

    /// <summary>
    /// An administrator revoked the account's sessions or every session.
    /// </summary>
    [JsonStringEnumMemberName("revoked")]
    Revoked = 2,

    /// <summary>
    /// The account was restricted, suspended, deactivated, deleted, taken down or erased.
    /// </summary>
    [JsonStringEnumMemberName("account")]
    Account = 3,

    /// <summary>
    /// The deletion of its organization was requested (IDN-ORG-003).
    /// </summary>
    [JsonStringEnumMemberName("organization")]
    Organization = 4,

    /// <summary>
    /// The library ended it on a sign of compromise: a refresh token presented twice or
    /// a provider's security event.
    /// </summary>
    [JsonStringEnumMemberName("security")]
    Security = 5,
}
