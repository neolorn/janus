using System;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One grant of a materialised derivation a refresh or the drift check wrote or took
/// back, as the audit trail records it.
/// </summary>
/// <param name="Id">The row it is recorded as.</param>
/// <param name="Acting">The person who called the refresh, or nothing where a system principal did.</param>
/// <param name="Effective">The person on whose behalf, or nothing where a system principal did.</param>
/// <param name="BreakGlassReason">
/// The reason given at the use of the break-glass credential, where the refresh was
/// called in the session it opened, or nothing.
/// </param>
/// <param name="Principal">The system principal that called the refresh, where one did.</param>
/// <param name="Organization">The organization the grant is held in.</param>
/// <param name="Holder">Who holds the grant.</param>
/// <param name="Grant">The grant written or taken back.</param>
/// <param name="Role">The role the grant confers.</param>
/// <param name="Retracted">Whether the grant was taken back rather than written.</param>
/// <param name="At">When.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-003 and AUTHZ-DERIVE-005 (D-187, D-206, D-207, chapter 10
/// section 5.24).
/// </remarks>
internal sealed record CorrectedGrant(
    AuditRecordId Id,
    SubjectId? Acting,
    SubjectId? Effective,
    string? BreakGlassReason,
    SystemPrincipal? Principal,
    OrganizationId Organization,
    GrantSubject Holder,
    GrantId Grant,
    RoleName Role,
    bool Retracted,
    DateTimeOffset At);
