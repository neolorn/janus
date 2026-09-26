using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy;

/// <summary>
/// Where what the area does is recorded. The area holds no audit trail of its own, so
/// what it has to record it hands out through this.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-001, PRIV-CONS-007, IDN-AUD-001 and CONV-LAYOUT-001. The
/// entry carries codes and references, never a rendered sentence and never the
/// person's own values (PRIV-RET-004).
/// </remarks>
internal interface IPrivacyAudit
{
    /// <summary>
    /// Records one action of the area.
    /// </summary>
    /// <param name="action">What happened.</param>
    /// <param name="acting">Who did it, where a person did.</param>
    /// <param name="subject">Whose account it was done on, where it was done on one.</param>
    /// <param name="at">When.</param>
    /// <param name="details">The structured context of the entry.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        SubjectId? acting,
        SubjectId? subject,
        DateTimeOffset at,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records one action background work took, under the system principal it ran as
    /// (IDN-PRIN-001, INF-BG-002).
    /// </summary>
    /// <param name="action">What happened.</param>
    /// <param name="principal">The principal that took it, with its stated reason.</param>
    /// <param name="subject">Whose account it was done on, where it was done on one.</param>
    /// <param name="at">When.</param>
    /// <param name="details">The structured context of the entry.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordedAsync(
        AuditAction action,
        SystemPrincipal principal,
        SubjectId? subject,
        DateTimeOffset at,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken);
}
