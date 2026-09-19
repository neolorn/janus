using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What still stands between a principal and an action a capability otherwise allows.
/// </summary>
/// <remarks>Implements chapter 10 section 5.20, API-CAP-001, AUTHZ-GATE-005, D-153.</remarks>
public enum CapabilityResidual
{
    /// <summary>
    /// The action is gated and the session has not met the gate.
    /// </summary>
    [JsonStringEnumMemberName("stepup")]
    StepUp = 0,

    /// <summary>
    /// The session is downgraded and the principal signs in again.
    /// </summary>
    [JsonStringEnumMemberName("reauthenticate")]
    Reauthenticate = 1,

    /// <summary>
    /// A restriction stands in the way.
    /// </summary>
    [JsonStringEnumMemberName("restricted")]
    Restricted = 2,

    /// <summary>
    /// The processing needs a consent the subject has not given.
    /// </summary>
    [JsonStringEnumMemberName("consent")]
    Consent = 3,

    /// <summary>
    /// The account's state does not admit the action.
    /// </summary>
    [JsonStringEnumMemberName("accountstate")]
    AccountState = 4,
}
