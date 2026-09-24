using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Privacy;

namespace Janus.Storage.Privacy;

/// <summary>
/// What the privacy area leaves in the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-AUD-001, IDN-PRIN-001, PRIV-RET-001 and PRIV-RET-004. The entry carries codes and
/// references, never a rendered sentence and never the person's own values. What the
/// area records is administrative and evidential, so it falls under the security
/// retention rather than the routine one.
/// </remarks>
internal sealed class PrivacyAudit(IAuditStore records, TimeProvider time) : IPrivacyAudit
{
    /// <inheritdoc/>
    public async ValueTask RecordedAsync(
        AuditAction action,
        SubjectId? acting,
        SubjectId? subject,
        DateTimeOffset at,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    action,
                    at,
                    acting ?? default,
                    subject ?? acting ?? default,
                    organization: null,
                    details),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask RecordedAsync(
        AuditAction action,
        SystemPrincipal principal,
        SubjectId? subject,
        DateTimeOffset at,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    action,
                    at,
                    principal,
                    subject,
                    organization: null,
                    details),
                cancellationToken)
            .ConfigureAwait(false);
}
