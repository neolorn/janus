using System.Text.Json.Serialization;

namespace Janus.Hosting.Recovery;

/// <summary>
/// How recovery reads and writes, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements API-CONV-002 and CONV-DESIGN-006.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(RecoveryRequest))]
[JsonSerializable(typeof(CompleteRecoveryRequest))]
[JsonSerializable(typeof(EnrolmentRequest))]
[JsonSerializable(typeof(ReportLossRequest))]
[JsonSerializable(typeof(CancelLossRequest))]
[JsonSerializable(typeof(ApproveRecoveryRequest))]
[JsonSerializable(typeof(EnrolmentSessionView))]
[JsonSerializable(typeof(LossReportedView))]
[JsonSerializable(typeof(ApprovedRecoveryView))]
internal sealed partial class RecoveryJson : JsonSerializerContext;
