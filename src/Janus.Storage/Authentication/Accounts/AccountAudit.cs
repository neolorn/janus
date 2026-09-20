using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Accounts;

/// <summary>
/// What an account changed about itself, written to the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements REG-PROF-001, REG-PREF-001, IDN-AUD-001 and CONV-DESIGN-003. The entry
/// says which group changed and never what it changed to, so the trail carries no
/// personal attribute of the person it is about.
/// </remarks>
internal sealed class AccountAudit(IAuditStore records, TimeProvider time) : IAccountAudit
{
    private static readonly IReadOnlyDictionary<string, JsonElement> Nothing =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask RecordedAsync(
        AuditAction action,
        SubjectId acting,
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records.AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Routine,
                    action,
                    at,
                    acting,
                    subject,
                    organization: null,
                    Nothing),
                cancellationToken)
            .ConfigureAwait(false);
}
