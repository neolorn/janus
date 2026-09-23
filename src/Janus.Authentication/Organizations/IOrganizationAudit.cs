using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// Where a change to an organization's lifecycle, to its domain lock or to the
/// invitations into it is written down: who, which organization, what, when and why.
/// </summary>
/// <remarks>Implements IDN-ORG-003, REG-DOM-001, IDN-LIFE-009a and IDN-AUD-001.</remarks>
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
}
