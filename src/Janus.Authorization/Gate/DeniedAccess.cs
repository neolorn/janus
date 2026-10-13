using System;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One refusal as the trail records it: who asked, what they asked to do, the kind of
/// thing they asked it of, the organization the evaluation was scoped to, and the
/// grant that decided it.
/// </summary>
/// <param name="Correlation">The identifier the refusal is answered with.</param>
/// <param name="Acting">
/// Whose credentials the request was made under, or nothing where background work
/// asked.
/// </param>
/// <param name="Effective">
/// Whose identity the request was made under, or nothing where background work asked.
/// </param>
/// <param name="Principal">
/// The name of the system principal that asked, or nothing where a person asked.
/// </param>
/// <param name="PrincipalReason">
/// The reason that principal stated, or nothing where a person asked.
/// </param>
/// <param name="BreakGlassReason">
/// The reason given at the use of the break-glass credential, where the request was
/// made in the session it opened, or nothing.
/// </param>
/// <param name="Organization">
/// The organization the evaluation was scoped to, or nothing where the library holds
/// no record of the thing that was asked about.
/// </param>
/// <param name="Permission">What was asked for.</param>
/// <param name="Type">The kind of thing it was asked of.</param>
/// <param name="At">The instant the refusal happened.</param>
/// <param name="Grant">
/// The deny grant that decided the refusal, as an explanation names it, or nothing
/// where no grant matched.
/// </param>
/// <remarks>
/// Implements AUTHZ-CONCEAL-004, IDN-PRIN-001, CONV-LOG-005 and CONV-LOG-006. The
/// identifier handed back says nothing about the record: it is drawn the same way and
/// carried the same way whether the record exists, and what it resolves to is the
/// permission, the principal, which for background work is its name and stated reason
/// (AUTHZ-CONCEAL-004 AC3), and the grant the explanation named when the refusal was
/// made. The trail holds the nil subject under both identities beside a principal, as
/// it does for every other action of background work.
/// </remarks>
internal sealed record DeniedAccess(
    AuditRecordId Correlation,
    SubjectId? Acting,
    SubjectId? Effective,
    string? Principal,
    string? PrincipalReason,
    string? BreakGlassReason,
    OrganizationId? Organization,
    Permission Permission,
    ResourceType Type,
    DateTimeOffset At,
    ExplainedGrant? Grant)
{
    /// <summary>
    /// The actor the refusal counts against: background work by its principal's name,
    /// anyone else by the acting subject the trail records.
    /// </summary>
    /// <remarks>
    /// Implements AUTHZ-GATE-004 and the row <c>alerting.denials.threshold</c> of chapter
    /// 10 (D-183): each principal is an actor of its own, and the alert names it.
    /// </remarks>
    public string Actor => Principal ?? (Acting ?? default).ToString();
}
