using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Gate;
using Janus.Core;
using Janus.Storage.Identity.Audit;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// The refusals and the exports the gate records, over the <c>audit_records</c> table.
/// </summary>
/// <param name="connections">Where the statements take their connection from.</param>
/// <remarks>
/// Implements AUTHZ-CONCEAL-004, AUTHZ-GATE-004, OPS-ALERT-006, CONV-LOG-005 and
/// CONV-DESIGN-003. The record is written through the operation's own connection, so a
/// refusal on a path that opened no transaction stands on its own and one inside a
/// transaction is part of it. Nothing here changes or removes a row.
/// </remarks>
internal sealed class AccessAudit(DataConnections connections) : IAccessAudit
{
    private const string Permission = "permission";
    private const string ResourceType = "resourceType";
    private const string Resource = "resource";

    private static readonly AuditAction Denied = AuditActions.AccessDenied;

    private const string Append =
        """
        INSERT INTO identity.audit_records
            (id, category, occurred_at, action, acting_subject, effective_subject,
             organization, details)
        VALUES (@id, @category, @at, @action, @acting, @effective, @organization,
                CAST(@details AS jsonb));
        """;

    private const string AppendExport =
        """
        INSERT INTO identity.audit_records
            (id, category, occurred_at, action, acting_subject, effective_subject,
             organization, details, principal, principal_reason)
        VALUES (@id, @category, @at, @action, @acting, @effective, @organization,
                CAST(@details AS jsonb), @principal, @reason);
        """;

    private const string ById =
        """
        SELECT acting_subject AS "Acting",
               effective_subject AS "Effective",
               organization AS "Organization",
               occurred_at AS "At",
               details AS "Details"
        FROM identity.audit_records
        WHERE id = @id AND action = @action;
        """;

    private const string ByActor =
        """
        SELECT count(*)::int
        FROM identity.audit_records
        WHERE category = @category AND action = @action AND acting_subject = @acting
          AND occurred_at >= @from AND occurred_at < @until;
        """;

    private const string ByNobody =
        """
        SELECT count(*)::int
        FROM identity.audit_records
        WHERE category = @category AND action = @action AND acting_subject IS NULL
          AND occurred_at >= @from AND occurred_at < @until;
        """;

    /// <inheritdoc/>
    public async ValueTask RecordAsync(DeniedAccess denial, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(denial);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Append,
                new
                {
                    id = denial.Correlation.Value,
                    category = VocabularyConverter<AuditCategory>.Write(AuditCategory.Security),
                    at = denial.At.ToUniversalTime(),
                    action = Denied.ToString(),
                    acting = denial.Acting?.Value,
                    effective = denial.Effective?.Value,
                    organization = denial.Organization?.Value,
                    details = Written(denial),
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(ExportedAccess export, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(export);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                AppendExport,
                new
                {
                    id = export.Id.Value,
                    category = VocabularyConverter<AuditCategory>.Write(AuditCategory.Security),
                    at = export.At.ToUniversalTime(),
                    action = AuditActions.AccessExported.ToString(),

                    // A principal's export names the nil subject under both identities,
                    // as every row a system principal's work leaves does.
                    acting = (export.Acting ?? default).Value,
                    effective = (export.Effective ?? default).Value,
                    organization = export.Organization?.Value,
                    details = Written(export),
                    principal = export.Principal?.Name,
                    reason = export.Principal?.Reason,
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<DeniedAccess?> FindAsync(
        AuditRecordId correlation,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<RecordedDenial> rows = await ambient.Connection
            .QueryAsync<RecordedDenial>(new CommandDefinition(
                ById,
                new { id = correlation.Value, action = Denied.ToString() },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        RecordedDenial? row = rows.FirstOrDefault();

        return row is null ? null : Read(correlation, row);
    }

    /// <inheritdoc/>
    public async ValueTask<int> CountAsync(
        SubjectId? acting,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(
                acting is null ? ByNobody : ByActor,
                new
                {
                    category = VocabularyConverter<AuditCategory>.Write(AuditCategory.Security),
                    action = Denied.ToString(),
                    acting = acting?.Value,
                    from = from.ToUniversalTime(),
                    until = until.ToUniversalTime(),
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static string Written(DeniedAccess denial) => JsonSerializer.Serialize(
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [Permission] = JsonSerializer.SerializeToElement(denial.Permission.ToString()),
            [ResourceType] = JsonSerializer.SerializeToElement(denial.Type.ToString()),
        },
        AuditDocument.Default.DictionaryStringJsonElement);

    // OPS-ALERT-006, D-045: what was exported is the operation and the kind of record,
    // and the one record where the call named one; never what the rows held.
    private static string Written(ExportedAccess export)
    {
        var details = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [Permission] = JsonSerializer.SerializeToElement(export.Permission.ToString()),
            [ResourceType] = JsonSerializer.SerializeToElement(export.Type.ToString()),
        };

        if (export.Record is ResourceId record)
        {
            details[Resource] = JsonSerializer.SerializeToElement(record.ToString());
        }

        return JsonSerializer.Serialize(details, AuditDocument.Default.DictionaryStringJsonElement);
    }

    private static DeniedAccess Read(AuditRecordId correlation, RecordedDenial row)
    {
        Dictionary<string, JsonElement> details =
            JsonSerializer.Deserialize(row.Details, AuditDocument.Default.DictionaryStringJsonElement)
            ?? throw new InvalidOperationException("The recorded refusal carries no fields.");

        return new DeniedAccess(
            correlation,
            row.Acting is Guid acting ? new SubjectId(acting) : null,
            row.Effective is Guid effective ? new SubjectId(effective) : null,
            row.Organization is Guid organization ? new OrganizationId(organization) : null,
            Core.Permission.Parse(Field(details, Permission)),
            Core.ResourceType.Parse(Field(details, ResourceType)),
            row.At);
    }

    private static string Field(Dictionary<string, JsonElement> details, string name) =>
        details.TryGetValue(name, out JsonElement value)
            ? value.GetString() ?? throw Missing(name)
            : throw Missing(name);

    private static InvalidOperationException Missing(string name) =>
        new(string.Create(
            CultureInfo.InvariantCulture,
            $"The recorded refusal carries no '{name}' field."));

    // The columns as the row holds them, before the fields are read back.
    private sealed class RecordedDenial
    {
        public Guid? Acting { get; init; }

        public Guid? Effective { get; init; }

        public Guid? Organization { get; init; }

        public DateTimeOffset At { get; init; }

        public string Details { get; init; } = "{}";
    }
}
