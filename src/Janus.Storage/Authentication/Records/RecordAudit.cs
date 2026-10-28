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
/// Implements REC-COL-002, REC-COL-003, REC-ACC-001, REC-ACC-002 and chapter 10
/// section 5.
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

    /// <inheritdoc/>
    public async ValueTask EnteredAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        OrganizationId organization,
        bool ageAnswered,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    AuditActions.RecordEntered,
                    at,
                    acting,
                    record,
                    breakGlassReason,
                    organization,
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["ageAnswered"] = JsonSerializer.SerializeToElement(ageAnswered),
                    }),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask AccessedAsync(
        SubjectId record,
        OrganizationId organization,
        RecordAccessSessionId session,
        bool byReference,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    AuditActions.RecordAccessed,
                    at,
                    record,
                    record,
                    breakGlassReason: null,
                    organization,
                    new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["session"] = JsonSerializer.SerializeToElement(session.ToString()),
                        ["byReference"] = JsonSerializer.SerializeToElement(byReference),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
    /// <inheritdoc/>
    public async ValueTask ChangedAsync(
        SubjectId acting,
        string? breakGlassReason,
        SubjectId record,
        string field,
        string? reason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
        {
            ["field"] = JsonSerializer.SerializeToElement(field),
        };

        if (reason is not null)
        {
            details["reason"] = JsonSerializer.SerializeToElement(reason);
        }

        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Routine,
                    AuditActions.RecordChanged,
                    at,
                    acting,
                    record,
                    breakGlassReason,
                    organization: null,
                    details),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
