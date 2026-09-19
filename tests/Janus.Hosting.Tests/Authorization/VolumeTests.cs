using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What the planner does with the permission predicate once the deployment holds the
/// volumes AUTHZ-TEST-002 names.
/// </summary>
/// <param name="volume">The seeded deployment and the plan read over it.</param>
[Trait("kind", "integration")]
public sealed class VolumeTests(VolumeFixture volume) : IClassFixture<VolumeFixture>
{
    private const int Page = 50;

    // The two tables the predicate reads by, and the only two the criterion is about.
    // janus.role_permissions holds three rows here and is correctly read whole; reading
    // it by index would be the slower plan, so it is not asked for.
    private static readonly string[] Scanned =
    [
        "Seq Scan on grants",
        "Seq Scan on ancestry",
    ];

    // What each of the two is reached by instead: the partial index over a holder's
    // live grants, and the ancestry's own key.
    private static readonly string[] Indexed = ["ix_grants_live_holder", "pk_ancestry"];

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
            SELECT (SELECT count(*) FROM janus.resources) AS "Resources",
                   (SELECT count(*) FROM janus.grants) AS "Grants",
                   (SELECT count(*) FROM janus.grants WHERE revoked_at IS NOT NULL) AS "Revoked",
                   (SELECT count(*) FROM janus.accounts) AS "Principals",
                   (SELECT count(*) FROM janus.groups) AS "Groups",
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
        Assert.Contains("documents janus_authz_row", volume.Plan, StringComparison.Ordinal);
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

        Assert.All(
            Indexed,
            index => Assert.Contains(index, volume.Plan, StringComparison.Ordinal));
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
