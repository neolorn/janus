using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// Where a change to an organization's lifecycle is written down: who, which
/// organization, what, when and why.
/// </summary>
/// <remarks>Implements IDN-ORG-003 and IDN-AUD-001.</remarks>
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
}
