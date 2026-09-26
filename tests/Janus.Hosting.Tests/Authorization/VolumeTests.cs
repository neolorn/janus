using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What the planner does with the permission predicate and the reverse lookup once the
/// deployment holds the volumes AUTHZ-TEST-002 names.
/// </summary>
/// <param name="volume">The seeded deployment and the plans read over it.</param>
[Trait("kind", "integration")]
public sealed class VolumeTests(VolumeFixture volume) : IClassFixture<VolumeFixture>
{
    private const int Page = 50;

    // What the ancestry is reached by instead of being read whole: its own key.
    private const string Ancestry = "pk_ancestry";

    // OPS-DB-003: the partial index over a holder's live grants, which the grant lookup
    // reads by, and the reverse lookup's own over the live grants on a resource.
    private const string LiveHolder = "ix_grants_live_holder";

    private const string LiveResource = "ix_grants_live_resource";

    // The two tables the predicate reads by, and the only two the criterion is about.
    // identity.role_permissions holds three rows here and is correctly read whole; reading
    // it by index would be the slower plan, so it is not asked for. The same holds for the
    // one organization the reverse lookup reads the standing of.
    private static readonly string[] Scanned =
    [
        "Seq Scan on grants",
        "Seq Scan on ancestry",
    ];

    /// <summary>
    /// AUTHZ-TEST-002 AC1: the plan of the primary list query is captured over a
    /// deployment holding the volumes the criterion states.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_TEST_002_AC1_ThePlanIsCapturedAtTheStatedVolumesAsync()
    {
        await using NpgsqlConnection connection = await volume.OpenAsync();

        Counted counted = await connection.QuerySingleAsync<Counted>(new CommandDefinition(
            """
            SELECT (SELECT count(*) FROM identity.resources) AS "Resources",
                   (SELECT count(*) FROM identity.grants) AS "Grants",
                   (SELECT count(*) FROM identity.grants WHERE revoked_at IS NOT NULL) AS "Revoked",
                   (SELECT count(*) FROM identity.accounts) AS "Principals",
                   (SELECT count(*) FROM identity.groups) AS "Groups",
                   (SELECT count(*) FROM host.reviewers) AS "Reviewers";
            """,
            commandTimeout: 600,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(
            new Counted(
                ProductionVolume.Resources,
                ProductionVolume.Grants,
                ProductionVolume.Revoked,
                ProductionVolume.Principals,
                ProductionVolume.Groups,
                ProductionVolume.Reviewers),
            counted);

        Assert.Equal(Page, volume.Page);
        Assert.Contains("documents identity_authz_row", volume.Plan, StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTHZ-DERIVE-004 AC2: the derivation joins the host's own relation, and at the
    /// stated volumes it reaches it by the index its declared column carries rather
    /// than by reading the relation whole.
    /// </summary>
    [Fact]
    public void AUTHZ_DERIVE_004_AC2_TheDerivedRuleUsesAnIndex()
    {
        TestContext.Current.TestOutputHelper?.WriteLine(volume.Plan);

        Assert.DoesNotContain("Seq Scan on reviewers", volume.Plan, StringComparison.Ordinal);
        Assert.Contains("ix_reviewers_reviewer", volume.Plan, StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTHZ-TEST-002 AC2: the permission predicate reaches the grants and the ancestry
    /// by index rather than by reading either table whole.
    /// </summary>
    [Fact]
    public void AUTHZ_TEST_002_AC2_ThePermissionPredicateUsesAnIndex()
    {
        TestContext.Current.TestOutputHelper?.WriteLine(volume.Plan);

        Assert.All(
            Scanned,
            whole => Assert.DoesNotContain(whole, volume.Plan, StringComparison.Ordinal));

        Assert.Contains(Ancestry, volume.Plan, StringComparison.Ordinal);
    }

    /// <summary>
    /// OPS-DB-003 AC1: at production-scale volume the primary permission predicate reads
    /// the grants by the partial index the item names, the one that leaves revoked rows
    /// out, as the catalogue defines it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_DB_003_AC1_ThePrimaryPredicateUsesThePartialIndexOverLiveGrantsAsync()
    {
        await using NpgsqlConnection connection = await volume.OpenAsync();

        string defined = await connection.QuerySingleAsync<string>(new CommandDefinition(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'identity' AND indexname = @LiveHolder;",
            new { LiveHolder },
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(LiveHolder, volume.Plan, StringComparison.Ordinal);
        Assert.EndsWith("WHERE (revoked_at IS NULL)", defined, StringComparison.Ordinal);
    }

    /// <summary>
    /// OPS-DB-003 AC2: the reverse lookup reads the grants on one record by its own
    /// index, and reads neither the grants nor the ancestry whole, at the stated volumes
    /// and for a record the grants reach. Every read of that index seeks it by a
    /// condition; an index walked end to end under a filter is the table read whole by
    /// another name.
    /// </summary>
    [Fact]
    public void OPS_DB_003_AC2_TheReverseLookupReadsNoTableWhole()
    {
        TestContext.Current.TestOutputHelper?.WriteLine(volume.ReversePlan);

        string[] lines = volume.ReversePlan.Split(Environment.NewLine);
        int[] reads = [.. Enumerable.Range(0, lines.Length)
            .Where(at => lines[at].Contains("using " + LiveResource + " on grants", StringComparison.Ordinal))];

        // Every grant is on a container, one in each ten thousand on the record's own,
        // and none of those is revoked.
        Assert.Equal(ProductionVolume.Grants / ProductionVolume.Containers, volume.Reached);

        Assert.All(
            Scanned,
            whole => Assert.DoesNotContain(whole, volume.ReversePlan, StringComparison.Ordinal));

        Assert.NotEmpty(reads);
        Assert.All(
            reads,
            at => Assert.StartsWith("Index Cond:", lines[at + 1].Trim(), StringComparison.Ordinal));
    }

    // What the database holds once the fixture has written it, read back rather than
    // taken on trust: the volumes are the criterion, not the loops that produced them.
    private sealed record Counted(
        long Resources,
        long Grants,
        long Revoked,
        long Principals,
        long Groups,
        long Reviewers);
}
