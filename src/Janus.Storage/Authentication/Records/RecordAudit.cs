using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// What happened to a person record, over the audit log.
/// </summary>
/// <param name="records">The audit log.</param>
/// <param name="time">The clock each record's identifier is drawn from.</param>
/// <remarks>
/// Implements REC-COL-002 and chapter 10 section 5.
/// </remarks>
internal sealed class RecordAudit(IAuditStore records, TimeProvider time) : IRecordAudit
{
    /// <inheritdoc/>
    public async ValueTask CollectedAsync(
        SubjectId record,
        OrganizationId organization,
        bool created,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    AuditActions.RecordCollected,
                    at,
                    record,
                    record,
                    breakGlassReason: null,
                    organization,
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["created"] = JsonSerializer.SerializeToElement(created),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
