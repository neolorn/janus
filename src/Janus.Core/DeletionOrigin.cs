using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Why an account entered the deletion grace window, which decides what the subject is
/// told and who may cancel it.
/// </summary>
/// <remarks>Implements IDN-LIFE-003 and chapter 10 section 5.12b.</remarks>
public enum DeletionOrigin
{
    /// <summary>
    /// The account's owner asked for erasure. The deletion notice carries the cancel
    /// link.
    /// </summary>
    [JsonStringEnumMemberName("self")]
    Self = 0,

    /// <summary>
    /// A takedown was triggered. No notice and no cancel link reach the subject, and
    /// the only way back is the reversal operation.
    /// </summary>
    [JsonStringEnumMemberName("takedown")]
    Takedown = 1,

    /// <summary>
    /// An erasure request arrived out of band and was entered through the
    /// privacy-request queue. The subject is notified without a cancel link, because
    /// the requester may not control the subject's addresses.
    /// </summary>
    [JsonStringEnumMemberName("oob-request")]
    OutOfBandRequest = 2,
}
