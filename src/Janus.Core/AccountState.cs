using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The one state an account is in at any time.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-007 and chapter 10 section 5.1. There is no pending state: an
/// account is created active in one transaction at the terms step of the registration
/// session, and an incomplete registration is a session rather than an account.
/// </remarks>
public enum AccountState
{
    /// <summary>
    /// Normal. The account signs in.
    /// </summary>
    [JsonStringEnumMemberName("active")]
    Active = 0,

    /// <summary>
    /// Disabled by its owner or by an administrator. The account does not sign in.
    /// </summary>
    [JsonStringEnumMemberName("suspended")]
    Suspended = 1,

    /// <summary>
    /// Processing restricted at the data subject's request. The account signs in and
    /// reads its own data; nothing acts on it.
    /// </summary>
    [JsonStringEnumMemberName("restricted")]
    Restricted = 2,

    /// <summary>
    /// The grace window is running and is cancellable until it elapses. The account
    /// does not sign in.
    /// </summary>
    [JsonStringEnumMemberName("deleting")]
    Deleting = 3,

    /// <summary>
    /// Personal data is unreadable and the anonymised record is retained. The account
    /// does not sign in and its identifier still resolves.
    /// </summary>
    [JsonStringEnumMemberName("deleted")]
    Deleted = 4,
}
