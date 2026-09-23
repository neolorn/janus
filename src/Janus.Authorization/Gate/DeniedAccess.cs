using System;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One refusal as the trail records it: who asked, what they asked to do, the kind of
/// thing they asked it of, and the organization the evaluation was scoped to.
/// </summary>
/// <param name="Correlation">The identifier the refusal is answered with.</param>
/// <param name="Acting">
/// Whose credentials the request was made under, or nothing where the request was
/// made under none.
/// </param>
/// <param name="Effective">
/// Whose identity the request was made under, or nothing where it was made under
/// none.
/// </param>
/// <param name="Organization">
/// The organization the evaluation was scoped to, or nothing where the library holds
/// no record of the thing that was asked about.
/// </param>
/// <param name="Permission">What was asked for.</param>
/// <param name="Type">The kind of thing it was asked of.</param>
/// <param name="At">The instant the refusal happened.</param>
/// <remarks>
/// Implements AUTHZ-CONCEAL-004 and CONV-LOG-005. The identifier handed back says
/// nothing about the record: it is drawn the same way and carried the same way whether
/// the record exists, and what it resolves to is the permission and the principal,
/// which is nobody where the request named nobody (AUTHZ-CONCEAL-004 AC2).
/// </remarks>
internal sealed record DeniedAccess(
    AuditRecordId Correlation,
    SubjectId? Acting,
    SubjectId? Effective,
    OrganizationId? Organization,
    Permission Permission,
    ResourceType Type,
    DateTimeOffset At);
