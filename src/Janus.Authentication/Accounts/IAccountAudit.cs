using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// Where a change an account made to itself is recorded. The area holds no audit
/// trail of its own, so what it has to record it hands out through this.
/// </summary>
/// <remarks>
/// Implements REG-PROF-001, REG-PREF-001, IDN-AUD-001 and CONV-LAYOUT-001. The entry
/// says that a field changed and never what it changed to: the values are the
/// person's, and the trail is read by people who are not them.
/// </remarks>
internal interface IAccountAudit
{
    /// <summary>
    /// Records that an account changed something about itself.
    /// </summary>
    /// <param name="action">What changed.</param>
    /// <param name="acting">Who made the change.</param>
    /// <param name="subject">Whose account it was made on.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        SubjectId acting,
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
