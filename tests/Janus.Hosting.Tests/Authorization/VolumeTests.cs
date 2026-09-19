using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What the planner does with the permission predicate once the deployment holds the
/// volumes AUTHZ-TEST-002 names.
/// </summary>
/// <param name="host">The deployment the rows are written to.</param>
[Trait("kind", "integration")]
public sealed class VolumeTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const int Page = 50;

    private static readonly ResourceType Document = ResourceType.Parse("document");

    // The two tables the predicate reads by, and the only two the criterion is about.
    // janus.role_permissions holds two rows here and is correctly read whole; reading
    // it by index would be the slower plan, so it is not asked for.
    private static readonly string[] Scanned =
    [
        "Seq Scan on grants",
        "Seq Scan on ancestry",
    ];

    /// <summary>
    /// AUTHZ-TEST-002 AC1, AC2: the plan of the primary list query is read at the
    /// stated volumes, and the permission predicate reaches the grants and the ancestry
    /// by index rather than by reading either table whole.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_TEST_002_AC1_TheListQueryPlanAtProductionVolumeUsesAnIndexAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var volume = new ProductionVolume(host);
        await volume.SeedAsync(cancellationToken);

        SqlFilter fragment = await FragmentAsync(volume, cancellationToken);
        string listing = Listing(fragment);

        await using NpgsqlConnection connection = await host.OpenAsync();

        await VolumesAsync(connection, cancellationToken);

        // A plan over a predicate that admits nothing says nothing, so the page the
        // plan is read for is the page the listing would show.
        Assert.Equal(Page, await PageAsync(connection, fragment, listing, cancellationToken));

        string plan = await PlanAsync(connection, fragment, listing, cancellationToken);

        TestContext.Current.TestOutputHelper?.WriteLine(plan);

        Assert.All(
            Scanned,
            whole => Assert.DoesNotContain(whole, plan, StringComparison.Ordinal));
    }

    private static async Task VolumesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        Counted counted = await connection.QuerySingleAsync<Counted>(new CommandDefinition(
            """
            SELECT (SELECT count(*) FROM janus.resources) AS "Resources",
                   (SELECT count(*) FROM janus.grants) AS "Grants",
                   (SELECT count(*) FROM janus.grants WHERE revoked_at IS NOT NULL) AS "Revoked",
                   (SELECT count(*) FROM janus.accounts) AS "Principals",
                   (SELECT count(*) FROM janus.groups) AS "Groups";
            """,
            commandTimeout: 600,
            cancellationToken: cancellationToken));

        Assert.Equal(
            new Counted(
                ProductionVolume.Resources,
                ProductionVolume.Grants,
                ProductionVolume.Revoked,
                ProductionVolume.Principals,
                ProductionVolume.Groups),
            counted);
    }

    private static string Listing(SqlFilter fragment) => string.Create(
        CultureInfo.InvariantCulture,
        $"""
        SELECT janus_authz_row.id
        FROM host.documents AS janus_authz_row
        WHERE {fragment.Text}
        ORDER BY janus_authz_row.id
        LIMIT {Page};
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

    private async Task<SqlFilter> FragmentAsync(
        ProductionVolume volume,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result<SqlFilter> rendering = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .FragmentAsync(
                AccessContext.Of(volume.Reader),
                HostPermissions.Read,
                Document,
                volume.Organization,
                "janus_authz_row",
                "id",
                cancellationToken);

        return rendering.Match(
            fragment => fragment,
            error => throw new InvalidOperationException(error.Code.ToString()));
    }

    // What the database holds once the fixture has written it, read back rather than
    // taken on trust: the volumes are the criterion, not the loops that produced them.
    private sealed record Counted(
        long Resources,
        long Grants,
        long Revoked,
        long Principals,
        long Groups);
}
