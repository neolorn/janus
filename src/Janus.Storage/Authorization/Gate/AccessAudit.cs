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
/// <param name="context">The scope's context, read for the transaction in progress.</param>
/// <param name="connections">Where the statements take their connection from.</param>
/// <remarks>
/// Implements AUTHZ-CONCEAL-004, AUTHZ-GATE-004, OPS-ALERT-006, CONV-LOG-005, CONV-LOG-006
/// and CONV-DESIGN-003. Every row is written in the transaction of the scope this is
/// resolved in: a refusal's in the unit of work the gate opens for its record in a scope
/// of its own, outside any transaction the caller holds open, so a rollback of the
/// caller's work leaves it standing (D-166, D-183); an export's in the action's own.
/// Nothing here changes or removes a row.
/// </remarks>
internal sealed class AccessAudit(StoreContext context, DataConnections connections) : IAccessAudit
{
    // AUTHZ-CONCEAL-004, D-183: a refusal is written and then counted against its actor's
    // window, so two at once would each count the window without the other. The actor's
    // refusals are held for the rest of the transaction; no read takes this lock.
    private const string Hold =
        """
        SELECT pg_advisory_xact_lock(hashtextextended(
            'identity.audit_records/authz.access.denied/' || CAST(@acting AS text) || '/' || COALESCE(CAST(@principal AS text), ''),
            0));
        """;

    private const string Permission = "permission";
    private const string ResourceType = "resourceType";
    private const string Resource = "resource";

    // CONV-LOG-006: the grant that decided a refusal and its fields, spelled as the
    // explanation spells them (AUTHZ-GATE-004, D-153).
    private const string Grant = "grant";
    private const string Id = "id";
    private const string Kind = "kind";
    private const string HolderType = "subjectType";
    private const string Holder = "subjectId";
    private const string Role = "role";
    private const string Deny = "deny";
    private const string InheritedFrom = "inheritedFrom";
    private const string ContainerId = "resourceId";

    private static readonly AuditAction Denied = AuditActions.AccessDenied;

    private const string Append =
        """
        INSERT INTO identity.audit_records
            (id, category, occurred_at, action, acting_subject, effective_subject,
             organization, details, principal, principal_reason, breakglass_reason)
        VALUES (@id, @category, @at, @action, @acting, @effective, @organization,
                CAST(@details AS jsonb), @principal, @reason, @breakGlassReason);
        """;

    private const string ById =
        """
        SELECT acting_subject AS "Acting",
               effective_subject AS "Effective",
               principal AS "Principal",
               principal_reason AS "PrincipalReason",
               breakglass_reason AS "BreakGlassReason",
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
          AND principal IS NULL
          AND occurred_at >= @from AND occurred_at < @until;
        """;

    // Background work records the nil subject, so the principal's rows are found through
    // the index on the acting subject and told apart by the name.
    private const string ByPrincipal =
        """
        SELECT count(*)::int
        FROM identity.audit_records
        WHERE category = @category AND action = @action AND acting_subject = @acting
          AND principal = @principal
          AND occurred_at >= @from AND occurred_at < @until;
        """;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask HoldAsync(SubjectId acting, string? principal, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An actor's refusals are held only inside the transaction of their record.");
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Hold,
                new { acting = acting.Value, principal },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

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

                    // AUTHZ-CONCEAL-004, IDN-AUD-001: a refusal of background work names
                    // the nil subject under both identities beside its principal, as
                    // every row its work leaves does.
                    acting = (denial.Acting ?? default).Value,
                    effective = (denial.Effective ?? default).Value,
                    principal = denial.Principal,
                    reason = denial.PrincipalReason,
                    breakGlassReason = denial.BreakGlassReason,
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
                Append,
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
                    breakGlassReason = export.BreakGlassReason,
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(CorrectedGrant corrected, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corrected);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        AuditAction action = corrected.Retracted ? AuditActions.GrantRetracted : AuditActions.GrantMaterialised;

        await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Append,
                new
                {
                    id = corrected.Id.Value,
                    category = VocabularyConverter<AuditCategory>.Write(AuditCategory.Security),
                    at = corrected.At.ToUniversalTime(),
                    action = action.ToString(),

                    // IDN-AUD-001: the drift check is no account, so the row names the
                    // nil subject under both identities beside its principal.
                    acting = default(SubjectId).Value,
                    effective = default(SubjectId).Value,
                    organization = (Guid?)corrected.Organization.Value,
                    details = Written(corrected),
                    principal = corrected.Principal.Name,
                    reason = corrected.Principal.Reason,
                    breakGlassReason = (string?)null,
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
        SubjectId acting,
        string? principal,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        return await ambient.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(
                principal is null ? ByActor : ByPrincipal,
                new
                {
                    category = VocabularyConverter<AuditCategory>.Write(AuditCategory.Security),
                    action = Denied.ToString(),
                    acting = acting.Value,
                    principal,
                    from = from.ToUniversalTime(),
                    until = until.ToUniversalTime(),
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    // CONV-LOG-006: the grant that decided the refusal is written with it, in the
    // values of the explanation (AUTHZ-GATE-004, D-153), so what the refusal resolves to
    // is what the gate explained.
    private static string Written(DeniedAccess denial)
    {
        var details = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [Permission] = JsonSerializer.SerializeToElement(denial.Permission.ToString()),
            [ResourceType] = JsonSerializer.SerializeToElement(denial.Type.ToString()),
        };

        if (denial.Grant is ExplainedGrant grant)
        {
            details[Grant] = Element(new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [Id] = JsonSerializer.SerializeToElement(grant.Id?.Value),
                [Kind] = JsonSerializer.SerializeToElement(VocabularyConverter<GrantKind>.Write(grant.Kind)),
                [HolderType] = JsonSerializer.SerializeToElement(VocabularyConverter<SubjectType>.Write(grant.SubjectType)),
                [Holder] = JsonSerializer.SerializeToElement(grant.SubjectId),
                [Role] = JsonSerializer.SerializeToElement(grant.Role.ToString()),
                [Deny] = JsonSerializer.SerializeToElement(grant.Deny),
                [InheritedFrom] = grant.InheritedFrom is ResourceReference container
                    ? Element(new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        [ResourceType] = JsonSerializer.SerializeToElement(container.Type.ToString()),
                        [ContainerId] = JsonSerializer.SerializeToElement(container.Id.ToString()),
                    })
                    : JsonSerializer.SerializeToElement<string?>(null),
            });
        }

        return JsonSerializer.Serialize(details, AuditDocument.Default.DictionaryStringJsonElement);
    }

    private static JsonElement Element(Dictionary<string, JsonElement> fields) =>
        JsonSerializer.SerializeToElement(fields, AuditDocument.Default.DictionaryStringJsonElement);

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

    // AUTHZ-GRANT-003, chapter 10 section 5.24: the grant the drift check wrote or took
    // back, and the role it confers.
    private static string Written(CorrectedGrant corrected) =>
        JsonSerializer.Serialize(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [Grant] = JsonSerializer.SerializeToElement(corrected.Grant.Value),
                [Role] = JsonSerializer.SerializeToElement(corrected.Role.ToString()),
            },
            AuditDocument.Default.DictionaryStringJsonElement);

    private static DeniedAccess Read(AuditRecordId correlation, RecordedDenial row)
    {
        Dictionary<string, JsonElement> details =
            JsonSerializer.Deserialize(row.Details, AuditDocument.Default.DictionaryStringJsonElement)
            ?? throw new InvalidOperationException("The recorded refusal carries no fields.");

        // The nil subject beside a principal stands for no account, so the refusal reads
        // back as the gate made it: under no identity, by the principal named.
        bool ofAPerson = row.Principal is null;

        return new DeniedAccess(
            correlation,
            ofAPerson ? new SubjectId(row.Acting) : null,
            ofAPerson ? new SubjectId(row.Effective) : null,
            row.Principal,
            row.PrincipalReason,
            row.BreakGlassReason,
            row.Organization is Guid organization ? new OrganizationId(organization) : null,
            Core.Permission.Parse(Field(details, Permission)),
            Core.ResourceType.Parse(Field(details, ResourceType)),
            row.At,
            details.TryGetValue(Grant, out JsonElement grant) ? Decided(grant) : null);
    }

    // The grant as the refusal wrote it. A refusal no grant decided carries none.
    private static ExplainedGrant Decided(JsonElement grant)
    {
        JsonElement id = grant.GetProperty(Id);
        JsonElement container = grant.GetProperty(InheritedFrom);

        return new ExplainedGrant(
            id.ValueKind is JsonValueKind.Null ? null : new GrantId(id.GetGuid()),
            VocabularyConverter<GrantKind>.Read(Text(grant, Kind)),
            VocabularyConverter<SubjectType>.Read(Text(grant, HolderType)),
            grant.GetProperty(Holder).GetGuid(),
            RoleName.Parse(Text(grant, Role)),
            grant.GetProperty(Deny).GetBoolean(),
            container.ValueKind is JsonValueKind.Null
                ? null
                : new ResourceReference(
                    Core.ResourceType.Parse(Text(container, ResourceType)),
                    ResourceId.Parse(Text(container, ContainerId))));
    }

    private static string Text(JsonElement fields, string name) =>
        fields.GetProperty(name).GetString() ?? throw Missing(name);

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
        public Guid Acting { get; init; }

        public Guid Effective { get; init; }

        public string? Principal { get; init; }

        public string? PrincipalReason { get; init; }

        public string? BreakGlassReason { get; init; }

        public Guid? Organization { get; init; }

        public DateTimeOffset At { get; init; }

        public string Details { get; init; } = "{}";
    }
}
