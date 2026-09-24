using System;
using System.Threading.Tasks;
using Dapper;
using Janus.Storage.Identity.Audit;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The audit trail's monthly partitions as the retention job reaches them: only over the
/// maintenance credential, the months ahead created and the expired ones dropped through
/// the functions the migration made (PRIV-RET-002, OPS-MIG-003a).
/// </summary>
[Trait("kind", "integration")]
public sealed class AuditPartitionsTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Maintenance = "identity_maintenance";

    private static readonly TimeSpan SecurityRetention = TimeSpan.FromDays(7 * 365);

    // Longer than any month the fixture holds has been past, so the one partition the case
    // makes expired is the only one the drop finds, whatever day the suite runs on.
    private static readonly TimeSpan RoutineRetention = TimeSpan.FromDays(30 * 365);

    /// <summary>
    /// OPS-MIG-003a: the maintenance credential is recognised, and neither the
    /// application's own nor a superuser, which holds the application's rights as it
    /// holds every role's, is taken for it.
    /// </summary>
    /// <param name="role">The role the connection runs as, or nothing for the superuser.</param>
    /// <param name="recognised">Whether it is taken for the maintenance credential.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(Maintenance, true)]
    [InlineData("identity_app", false)]
    [InlineData(null, false)]
    public async Task OPS_MIG_003a_OnlyTheMaintenanceCredentialIsTakenForItAsync(string? role, bool recognised)
    {
        await using StoreContext context = As(role);

        Assert.Equal(
            recognised,
            await new AuditPartitions(new DataConnections(context))
                .UnderMaintenanceCredentialAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// PRIV-RET-002 AC3: under the maintenance credential, a month ahead that is missing is
    /// created again, and a partition past its category's retention is dropped while the
    /// months in retention stand.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_002_AC3_TheMonthsAheadAreCreatedAndTheExpiredDroppedAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        string ahead = await connection.ExecuteScalarAsync<string>(
            """
            SELECT 'audit_records_routine_'
                || to_char(date_trunc('month', now() AT TIME ZONE 'UTC') + interval '2 month', 'YYYY_MM')
            """) ?? string.Empty;

        await connection.ExecuteAsync("DROP TABLE identity." + ahead);
        await connection.ExecuteAsync(
            """
            CREATE TABLE identity.audit_records_routine_1990_03
                PARTITION OF identity.audit_records_routine
                FOR VALUES FROM ('1990-03-01 00:00:00+00') TO ('1990-04-01 00:00:00+00');
            """);

        await using StoreContext context = As(Maintenance);
        var partitions = new AuditPartitions(new DataConnections(context));

        Assert.Equal(1, await partitions.EnsureAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await partitions.EnsureAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            1,
            await partitions.DropExpiredAsync(SecurityRetention, RoutineRetention, TestContext.Current.CancellationToken));

        Assert.Equal(ahead, await StandingAsync(connection, ahead));
        Assert.Null(await StandingAsync(connection, "audit_records_routine_1990_03"));
    }

    private static async Task<string?> StandingAsync(NpgsqlConnection connection, string partition) =>
        await connection.ExecuteScalarAsync<string>(
            "SELECT relname::text FROM pg_class WHERE relname = @partition",
            new { partition });

    private StoreContext As(string? role)
    {
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString);

        if (role is not null)
        {
            connection.Options = "-c role=" + role;
        }

        return new StoreContext(new DbContextOptionsBuilder<StoreContext>()
            .UseNpgsql(connection.ConnectionString)
            .Options);
    }
}
