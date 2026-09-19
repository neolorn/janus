using System.Text.Json.Serialization;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a gate asks of the session in front of it. A gate is never a bare refusal:
/// where nothing the account holds reaches it, the answer says what would.
/// </summary>
/// <remarks>Implements AUTH-STEP-002 and chapter 9 <c>POST /auth/step-up</c>.</remarks>
internal enum StepUpOutcome
{
    /// <summary>
    /// The session already proved what the gate asks, recently enough. Nothing is
    /// required.
    /// </summary>
    [JsonStringEnumMemberName("satisfied")]
    Satisfied = 0,

    /// <summary>
    /// One of the offered combinations reaches the gate and the subject chooses
    /// among them.
    /// </summary>
    [JsonStringEnumMemberName("present")]
    Present = 1,

    /// <summary>
    /// The account has never held what the gate asks, so the answer is to enrol, at
    /// that moment.
    /// </summary>
    [JsonStringEnumMemberName("enrol")]
    Enrol = 2,

    /// <summary>
    /// The account reaches the gate but what would satisfy it is no longer in its
    /// owner's hands, so the answer is to report the loss.
    /// </summary>
    [JsonStringEnumMemberName("report-loss")]
    ReportLoss = 3,

    /// <summary>
    /// A loss report is already pending: the gate stays closed until invalidation
    /// completes, and the answer says when that is.
    /// </summary>
    [JsonStringEnumMemberName("pending")]
    LossPending = 4,
}
