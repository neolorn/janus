using System.Text.Json.Serialization;

namespace Janus.Authentication.Maintenance;

/// <summary>
/// The recurring human tasks and reviews of chapter 06 section 9 that the maintenance
/// log records.
/// </summary>
/// <remarks>Implements OPS-MAINT-001 (D-153).</remarks>
internal enum MaintenanceTask
{
    /// <summary>
    /// The annual operation: the break-glass credential regenerated and resealed, the
    /// key-encryption and backup keys rotated, the envelope inspected, escrow tested.
    /// </summary>
    [JsonStringEnumMemberName("envelope-rotation")]
    EnvelopeRotation = 0,

    /// <summary>The regulatory licence or a permit renewed.</summary>
    [JsonStringEnumMemberName("licence-renewal")]
    LicenceRenewal = 1,

    /// <summary>The weekly review of the approver report.</summary>
    [JsonStringEnumMemberName("approver-review")]
    ApproverReview = 2,

    /// <summary>The monthly review of pipeline consumption.</summary>
    [JsonStringEnumMemberName("pipeline-consumption-review")]
    PipelineConsumptionReview = 3,

    /// <summary>The quarterly review of the accepted-risk triggers.</summary>
    [JsonStringEnumMemberName("risk-trigger-review")]
    RiskTriggerReview = 4,
}
