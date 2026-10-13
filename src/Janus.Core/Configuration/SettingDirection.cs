using System.Text.Json.Serialization;

namespace Janus.Core.Configuration;

/// <summary>
/// Which way a change to a setting loosens the deployment. Tightening a control is
/// free; loosening one costs step-up, a written reason and an audit entry.
/// </summary>
/// <remarks>
/// Implements OPS-CFG-002, chapter 10 section 4. The administration interface writes a
/// member as chapter 09 section 8 spells it (D-166).
/// </remarks>
public enum SettingDirection
{
    /// <summary>
    /// A larger value, a longer window or an added member loosens: lengthening a
    /// session lifetime, adding a login factor, adding a locked domain.
    /// </summary>
    [JsonStringEnumMemberName("increase")]
    Increase = 0,

    /// <summary>
    /// A smaller value, a shorter window, a removed member or turning a control off
    /// loosens: lowering a password floor, dropping a rejection source, disabling the
    /// new-device check.
    /// </summary>
    [JsonStringEnumMemberName("decrease")]
    Decrease = 1,

    /// <summary>
    /// The setting has no direction, so every change is treated as a loosening:
    /// alert destinations, approver counts, template content.
    /// </summary>
    [JsonStringEnumMemberName("any-change")]
    AnyChange = 2,
}
