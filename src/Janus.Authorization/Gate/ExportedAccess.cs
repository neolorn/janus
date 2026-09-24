using System;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One export operation the gate admitted, as the audit trail records it.
/// </summary>
/// <param name="Id">The row it is recorded as.</param>
/// <param name="Acting">The person who exported, or nothing where a system principal did.</param>
/// <param name="Effective">The person on whose behalf, or nothing where a system principal did.</param>
/// <param name="Principal">The system principal that exported, where one did.</param>
/// <param name="Organization">The organization the export was within, where the call named one.</param>
/// <param name="Permission">The export operation.</param>
/// <param name="Type">The kind of record exported.</param>
/// <param name="Record">The one record exported, where the call named one rather than a list.</param>
/// <param name="At">When.</param>
/// <remarks>Implements OPS-ALERT-006 and D-045: who, what and when, individually.</remarks>
internal sealed record ExportedAccess(
    AuditRecordId Id,
    SubjectId? Acting,
    SubjectId? Effective,
    SystemPrincipal? Principal,
    OrganizationId? Organization,
    Permission Permission,
    ResourceType Type,
    ResourceId? Record,
    DateTimeOffset At);
