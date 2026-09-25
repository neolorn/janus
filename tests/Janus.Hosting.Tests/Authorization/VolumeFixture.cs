using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Grants;
using Janus.Core;
using Janus.Storage;
using Janus.Storage.Authorization.Grants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A deployment holding the volumes AUTHZ-TEST-002 names, with the plans of the primary
/// list query and of the reverse lookup read once over it.
/// </summary>
/// <remarks>
/// Implements CONV-TEST-002. Writing the rows is minutes of work, so the deployment and
/// the plan belong to the class rather than to a test.
/// </remarks>
public sealed class VolumeFixture : IAsyncLifetime
{
    private const int Size = 50;

    private static readonly ResourceType Document = ResourceType.Parse("document");

    // The first record of the first container, which the container's grants reach.
    private static readonly ResourceReference Record = new(Document, ResourceId.Parse("document-0000000"));

    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly HostFixture _host = new();

    /// <summary>
    /// How many records the page returned. A plan over a predicate that admits nothing
    /// says nothing, so the page the plan is read for is the page the listing shows.
    /// </summary>
    public int Page { get; private set; }

    /// <summary>
    /// The plan of the primary list query, as the database reported it.
    /// </summary>
    public string Plan { get; private set; } = string.Empty;

    /// <summary>
    /// How many grants the reverse lookup read on the record. A plan over a read that
    /// finds nothing says nothing, so the record is one the grants reach.
    /// </summary>
    public int Reached { get; private set; }

    /// <summary>
    /// The plan of the statement the reverse lookup reads the grants on one record by,
    /// as the database reported it for the values the statement was sent with.
    /// </summary>
    public string ReversePlan { get; private set; } = string.Empty;

    /// <summary>
    /// Opens a connection to the seeded deployment.
    /// </summary>
    /// <returns>The open connection.</returns>
    public async ValueTask<NpgsqlConnection> OpenAsync() => await _host.OpenAsync();

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await _host.InitializeAsync();

        var volume = new ProductionVolume(_host);
        await volume.SeedAsync(cancellationToken);

        SqlFilter fragment = await FragmentAsync(volume, cancellationToken);
        string listing = Listing(fragment);

        await using NpgsqlConnection connection = await _host.OpenAsync();

        Page = await PageAsync(connection, fragment, listing, cancellationToken);
        Plan = await PlanAsync(connection, fragment, listing, cancellationToken);

        CapturedCommand sent = await ReverseLookupAsync(volume, cancellationToken);

        ReversePlan = await ExplainedAsync(connection, sent, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    private static string Listing(SqlFilter fragment) => string.Create(
        CultureInfo.InvariantCulture,
        $"""
        SELECT identity_authz_row.id
        FROM host.documents AS identity_authz_row
        WHERE {fragment.Text}
        ORDER BY identity_authz_row.id
        LIMIT {Size};
        """);

    private static DynamicParameters Arguments(SqlFilter fragment)
    {
        var arguments = new DynamicParameters();

        foreach (KeyValuePair<string, object> parameter in fragment.Parameters)
        {
            arguments.Add(parameter.Key, parameter.Value);
        }

        return arguments;
    }

    private static async Task<int> PageAsync(
        NpgsqlConnection connection,
        SqlFilter fragment,
        string listing,
        CancellationToken cancellationToken)
    {
        IEnumerable<string> page = await connection.QueryAsync<string>(new CommandDefinition(
            listing,
            Arguments(fragment),
            commandTimeout: 600,
            cancellationToken: cancellationToken));

        return page.Count();
    }

    private static async Task<string> PlanAsync(
        NpgsqlConnection connection,
        SqlFilter fragment,
        string listing,
        CancellationToken cancellationToken)
    {
        IEnumerable<string> lines = await connection.QueryAsync<string>(new CommandDefinition(
            "EXPLAIN (ANALYZE, BUFFERS) " + listing,
            Arguments(fragment),
            commandTimeout: 600,
            cancellationToken: cancellationToken));

        return string.Join(Environment.NewLine, lines);
    }

    // OPS-DB-003 AC2: the statement is the one the store sends, read where it leaves the
    // context, so the plan is of what the library runs and not of a copy of it.
    private static async Task<string> ExplainedAsync(
        NpgsqlConnection connection,
        CapturedCommand sent,
        CancellationToken cancellationToken)
    {
        var arguments = new DynamicParameters();

        foreach (NpgsqlParameter parameter in sent.Parameters)
        {
            arguments.Add(parameter.ParameterName, parameter.Value, parameter.DbType);
        }

        IEnumerable<string> lines = await connection.QueryAsync<string>(new CommandDefinition(
            "EXPLAIN (ANALYZE, BUFFERS) " + sent.Text,
            arguments,
            commandTimeout: 600,
            cancellationToken: cancellationToken));

        return string.Join(Environment.NewLine, lines);
    }

    private async Task<CapturedCommand> ReverseLookupAsync(
        ProductionVolume volume,
        CancellationToken cancellationToken)
    {
        var sent = new CapturedCommand();

        await using var context = new StoreContext(new DbContextOptionsBuilder<StoreContext>()
            .UseNpgsql(_host.ConnectionString)
            .AddInterceptors(sent)
            .Options);

        IReadOnlyList<Grant> reached = await new GrantStore(context, new DataConnections(context))
            .OnAsync(Record, volume.Organization, Now, cancellationToken);

        Reached = reached.Count;

        return sent;
    }

    private async Task<SqlFilter> FragmentAsync(
        ProductionVolume volume,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _host.Services.CreateAsyncScope();

        Result<SqlFilter> rendering = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .FragmentAsync(
                AccessContext.Of(volume.Reader),
                HostPermissions.Read,
                Document,
                volume.Organization,
                "identity_authz_row",
                "id",
                cancellationToken);

        return rendering.Match(
            fragment => fragment,
            error => throw new InvalidOperationException(error.Code.ToString()));
    }
}
