using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Identity.Audit;
using Janus.Storage.Identity.Audit;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Configuration;

/// <summary>
/// What a runtime configuration change leaves in the audit trail, and how it is read
/// back.
/// </summary>
/// <param name="context">The context the read runs on.</param>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-CFG-002, OPS-CFG-005 and CONV-DESIGN-003. The record goes to the one
/// trail under the security retention, exactly as a permission grant does, and the two
/// readings the chapter asks for are the two indexes the table carries.
/// </remarks>
internal sealed class ConfigurationAudit(
    StoreContext context,
    IAuditStore records,
    TimeProvider time) : IConfigurationAudit
{
    private static readonly AuditAction Changed = AuditActions.ConfigurationChanged;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The change is absent.</exception>
    public async ValueTask ChangedAsync(ConfigurationChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        var details = new Dictionary<string, JsonElement>(capacity: 5, StringComparer.Ordinal)
        {
            ["key"] = JsonSerializer.SerializeToElement(change.Key.ToString()),
            ["before"] = JsonSerializer.SerializeToElement(change.Before),
            ["after"] = JsonSerializer.SerializeToElement(change.After),
            ["loosening"] = JsonSerializer.SerializeToElement(change.Loosening),
        };

        if (change.Reason is not null)
        {
            details["reason"] = JsonSerializer.SerializeToElement(change.Reason);
        }

        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    Changed,
                    change.At,
                    change.Actor,
                    change.Actor,
                    organization: null,
                    details),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ConfigurationChange>> OfSettingAsync(
        ConfigurationKey key,
        CancellationToken cancellationToken) =>
        await ReadAsync(
                Changes().Where(row => EF.Functions.JsonContains(row.Details, Naming(key))),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ConfigurationChange>> OfActorAsync(
        SubjectId actor,
        CancellationToken cancellationToken) =>
        await ReadAsync(Changes().Where(row => row.ActingSubject == actor), cancellationToken)
            .ConfigureAwait(false);

    private static string Naming(ConfigurationKey key) =>
        JsonSerializer.Serialize(
            new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
            {
                ["key"] = key.ToString(),
            },
            AuditQuery.Default.DictionaryStringString);

    private static ConfigurationChange Read(AuditRowRecord row)
    {
        using var details = JsonDocument.Parse(row.Details);
        JsonElement fields = details.RootElement;

        return new ConfigurationChange(
            ConfigurationKey.Parse(fields.GetProperty("key").GetString()!),
            fields.GetProperty("before").GetString()!,
            fields.GetProperty("after").GetString()!,
            fields.GetProperty("loosening").GetBoolean(),
            fields.TryGetProperty("reason", out JsonElement reason) ? reason.GetString() : null,
            row.ActingSubject,
            row.OccurredAt);
    }

    private static async ValueTask<IReadOnlyList<ConfigurationChange>> ReadAsync(
        IQueryable<AuditRowRecord> rows,
        CancellationToken cancellationToken)
    {
        List<AuditRowRecord> found = await rows
            .OrderByDescending(row => row.OccurredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var changes = new List<ConfigurationChange>(found.Count);

        foreach (AuditRowRecord row in found)
        {
            changes.Add(Read(row));
        }

        return changes;
    }

    private IQueryable<AuditRowRecord> Changes() =>
        context.AuditRecords.Where(row => row.Action == Changed);
}
