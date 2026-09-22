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
/// The refusals the gate records, over the <c>audit_records</c> table.
/// </summary>
/// <param name="connections">Where the statements take their connection from.</param>
/// <remarks>
/// Implements AUTHZ-CONCEAL-004, AUTHZ-GATE-004, CONV-LOG-005 and CONV-DESIGN-003. The
/// record is written through the operation's own connection, so a refusal on a path
/// that opened no transaction stands on its own and one inside a transaction is part of
/// it. Nothing here changes or removes a row.
/// </remarks>
internal sealed class AccessAudit(DataConnections connections) : IAccessAudit
{
    private const string Permission = "permission";
    private const string ResourceType = "resourceType";

    private static readonly AuditAction Denied = AuditAction.Parse("authz.access.denied");

    private const string Append =
        """
        INSERT INTO janus.audit_records
            (id, category, occurred_at, action, acting_subject, effective_subject,
             organization, details)
        VALUES (@id, @category, @at, @action, @acting, @effective, @organization,
                CAST(@details AS jsonb));
        """;

    private const string ById =
        """
        SELECT acting_subject AS "Acting",
               effective_subject AS "Effective",
               organization AS "Organization",
               occurred_at AS "At",
               details AS "Details"
        FROM janus.audit_records
        WHERE id = @id AND action = @action;
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

    private static string Written(DeniedAccess denial) => JsonSerializer.Serialize(
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [Permission] = JsonSerializer.SerializeToElement(denial.Permission.ToString()),
            [ResourceType] = JsonSerializer.SerializeToElement(denial.Type.ToString()),
        },
        AuditDocument.Default.DictionaryStringJsonElement);

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
