using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Roles;

/// <summary>
/// Where a change to a role is written down: who, what it was, what it became, when
/// and why.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004, OPS-CFG-007 and IDN-AUD-001. A role changes what every
/// holder of it may do at once, so the change is recorded as a grant is.
/// </remarks>
internal interface IRoleAudit
{
    /// <summary>
    /// Records a role created, or the permissions of one changed.
    /// </summary>
    /// <param name="role">Which role.</param>
    /// <param name="before">What it permitted, or nothing where it is new.</param>
    /// <param name="after">What it permits now.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who defined it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask DefinedAsync(
        RoleName role,
        IReadOnlyList<Permission>? before,
        IReadOnlyList<Permission> after,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a role removed.
    /// </summary>
    /// <param name="role">Which role.</param>
    /// <param name="before">What it permitted.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who removed it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RemovedAsync(
        RoleName role,
        IReadOnlyList<Permission> before,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
