using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// Runs a rendered rule against the library's own record of the host's resources.
/// </summary>
/// <param name="connections">Where the statements take their connection from.</param>
/// <remarks>
/// Implements AUTHZ-GATE-002, AUTHZ-PRIN-001, AUTHZ-PRIN-002 and CONV-DESIGN-003. The
/// statements are the renderer's own text with the record carried as a parameter; no
/// value reaches the text (AUTHZ-GATE-002 AC3).
/// </remarks>
internal sealed class AccessEvaluator(DataConnections connections) : IAccessEvaluator
{

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CandidateGrant>> CandidatesAsync(
        SqlFilter candidates,
        ResourceId resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<MatchedGrant> matched = await ambient.Connection
            .QueryAsync<MatchedGrant>(new CommandDefinition(
                candidates.Text,
                Arguments(candidates, [(PermissionRule.RecordParameter, resource.ToString())]),
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return [.. matched.Select(Read)];
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CandidateGrant>> OrganizationCandidatesAsync(
        SqlFilter candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<MatchedGrant> matched = await ambient.Connection
            .QueryAsync<MatchedGrant>(new CommandDefinition(
                candidates.Text,
                Arguments(candidates, []),
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return [.. matched.Select(Read)];
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PageCapability>> PageAsync(
        SqlFilter page,
        IReadOnlyList<ResourceId> resources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(resources);

        if (resources.Count == 0)
        {
            return [];
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<PageCapability> conferred = await ambient.Connection
            .QueryAsync<PageCapability>(new CommandDefinition(
                page.Text,
                Arguments(
                    page,
                    [(PermissionRule.PageParameter, resources.Select(id => id.ToString()).ToArray())]),
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return [.. conferred];
    }

    // CONV-ENUM-001: the columns are constrained text, so the vocabularies are read
    // back through the one converter that writes them.
    private static CandidateGrant Read(MatchedGrant row) => new()
    {
        Grant = row.Grant,
        Kind = VocabularyConverter<GrantKind>.Read(row.Kind),
        SubjectType = VocabularyConverter<SubjectType>.Read(row.SubjectType),
        SubjectId = row.SubjectId,
        Role = RoleName.Parse(row.Role),
        Deny = row.Deny,
        AncestorType = row.AncestorType,
        AncestorId = row.AncestorId,
    };

    private static DynamicParameters Arguments(
        SqlFilter filter,
        IReadOnlyList<(string Name, object Value)> own)
    {
        var arguments = new DynamicParameters();

        foreach (KeyValuePair<string, object> parameter in filter.Parameters)
        {
            arguments.Add(parameter.Key, parameter.Value);
        }

        foreach ((string name, object value) in own)
        {
            arguments.Add(name, value, value is string ? DbType.String : null);
        }

        return arguments;
    }

    // The columns as the view holds them, before the constrained text is read back as
    // the member it stands for.
    private sealed class MatchedGrant
    {
        public Guid Grant { get; init; }

        public string Kind { get; init; } = string.Empty;

        public string SubjectType { get; init; } = string.Empty;

        public Guid SubjectId { get; init; }

        public string Role { get; init; } = string.Empty;

        public bool Deny { get; init; }

        public string? AncestorType { get; init; }

        public string? AncestorId { get; init; }
    }
}
