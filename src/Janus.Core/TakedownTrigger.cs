using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What raised the credible indication that a customer is under age, recorded beside
/// the written reason.
/// </summary>
/// <remarks>Implements IDN-LIFE-003 and chapter 10 section 5.12d.</remarks>
public enum TakedownTrigger
{
    /// <summary>
    /// A member of staff observed the condition.
    /// </summary>
    [JsonStringEnumMemberName("staff-report")]
    StaffReport = 0,

    /// <summary>
    /// Another customer reported it.
    /// </summary>
    [JsonStringEnumMemberName("customer-report")]
    CustomerReport = 1,

    /// <summary>
    /// A host-declared rule or screening raised it.
    /// </summary>
    [JsonStringEnumMemberName("automated-signal")]
    AutomatedSignal = 2,

    /// <summary>
    /// A competent authority asked.
    /// </summary>
    [JsonStringEnumMemberName("authority-request")]
    AuthorityRequest = 3,
}
