using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// Where a change to an organization's lifecycle, to its domain lock, to the
/// invitations into it or to its memberships is written down: who, which organization,
/// what, when and why.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-003, REG-DOM-001, IDN-LIFE-009a, IDN-MEM-001, IDN-AUD-001 and
/// IDN-PRIN-001.
/// </remarks>
internal interface IOrganizationAudit
{
    /// <summary>
    /// Records a change to an organization's lifecycle.
    /// </summary>
    /// <param name="action">What changed.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        OrganizationId organization,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a system principal did something to an organization, as bootstrap
    /// creates the administrative one (IDN-PRIN-001).
    /// </summary>
    /// <param name="action">What happened.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="principal">The principal that did it, with its stated reason.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        OrganizationId organization,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a change to one domain of an organization's lock.
    /// </summary>
    /// <param name="action">What changed.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="domain">Which domain.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask DomainChangedAsync(
        AuditAction action,
        OrganizationId organization,
        string domain,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records an invitation issued into an organization, or one revoked. What the
    /// invitation binds is someone's personal data before any account of theirs exists,
    /// so the record names the invitation and nothing it binds.
    /// </summary>
    /// <param name="action">What changed.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="invitation">Which invitation.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask InvitationChangedAsync(
        AuditAction action,
        OrganizationId organization,
        InvitationId invitation,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a membership an administrator ended, filed under the organization with
    /// the member as whom it was done to.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="membership">Which membership.</param>
    /// <param name="member">Whose membership it was.</param>
    /// <param name="actor">Who ended it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask MembershipEndedAsync(
        OrganizationId organization,
        MembershipId membership,
        SubjectId member,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
