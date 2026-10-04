using System;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One grant of a materialised derivation the drift check wrote or took back, as the
/// audit trail records it.
/// </summary>
/// <param name="Id">The row it is recorded as.</param>
/// <param name="Principal">The system principal the drift check ran as.</param>
/// <param name="Organization">The organization the grant is held in.</param>
/// <param name="Grant">The grant written or taken back.</param>
/// <param name="Role">The role the grant confers.</param>
/// <param name="Retracted">Whether the grant was taken back rather than written.</param>
/// <param name="At">When.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-003 and AUTHZ-DERIVE-005 (D-187, chapter 10 section 5.24).
/// </remarks>
internal sealed record CorrectedGrant(
    AuditRecordId Id,
    SystemPrincipal Principal,
    OrganizationId Organization,
    GrantId Grant,
    RoleName Role,
    bool Retracted,
    DateTimeOffset At);
