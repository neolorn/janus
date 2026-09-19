using System.Text.Json.Serialization;

namespace Janus.Core.Configuration;

/// <summary>
/// A signal bot defence counts before it asks for a challenge. The set is closed until
/// a decision adds a member.
/// </summary>
/// <remarks>Implements chapter 10 section 4.5, AUTH-ABUSE-008, D-152.</remarks>
public enum BotDefenceSignal
{
    /// <summary>
    /// The request arrives from an address in a datacenter range.
    /// </summary>
    [JsonStringEnumMemberName("datacenterRange")]
    DatacenterRange = 0,

    /// <summary>
    /// More registration sessions from one source in an hour than
    /// <c>abuse.botdefence.repeatedattempts</c> admits.
    /// </summary>
    [JsonStringEnumMemberName("repeatedAttempts")]
    RepeatedAttempts = 1,
}
