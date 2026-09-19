using System;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// What each of the three database roles may do (OPS-MIG-003, OPS-MIG-003a,
/// PRIV-RET-002).
/// </summary>
/// <remarks>
/// The migration role owns the schema and is the role these tests run the migrations
/// under. The other two are reached with <c>SET ROLE</c>, which drops the session to
/// exactly the rights that role holds.
/// </remarks>
[Trait("kind", "integration")]
public sealed class DatabaseRoleTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    // The shipped defaults of retention.audit.security and retention.audit.routine,
    // which the worker reads from the catalogue and passes in.
    private const string Retentions = "interval '7 years', interval '90 days'";

    private const string InsufficientPrivilege = "42501";

    /// <summary>
    /// PRIV-RET-002 AC1: an update or a delete of an audit row issued by the
    /// application is refused, and the insert and the read it does need are not.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_002_AC1_TheApplicationCannotUpdateOrDeleteAnAuditRowAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("janus_app");

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("UPDATE janus.audit_records SET action = 'a.b'"));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("DELETE FROM janus.audit_records"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM janus.audit_records"));
    }

    /// <summary>
    /// PRIV-RET-002 AC3: a partition whose end has passed its category's retention is
    /// dropped without the application taking any part, and the months in retention are
    /// left where they are.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_002_AC3_AnExpiredPartitionIsDroppedAndTheOthersStandAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(
            """
            CREATE TABLE janus.audit_records_routine_2020_01
                PARTITION OF janus.audit_records_routine
                FOR VALUES FROM ('2020-01-01 00:00:00+00') TO ('2020-02-01 00:00:00+00');
            """);

        int standing = await PartitionsAsync(connection);
        int dropped = await connection.ExecuteScalarAsync<int>(
            "SELECT janus.audit_drop_expired_partitions(" + Retentions + ")");

        Assert.Equal(1, dropped);
        Assert.Equal(standing - 1, await PartitionsAsync(connection));
        Assert.Null(await connection.ExecuteScalarAsync<string>(
            "SELECT to_regclass('janus.audit_records_routine_2020_01')::text"));
    }

    /// <summary>
    /// PRIV-RET-002 AC5: the maintenance role executes the drop, and the application
    /// role cannot execute it.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_002_AC5_OnlyTheMaintenanceRoleExecutesTheDropAsync()
    {
        await using (NpgsqlConnection maintenance = await AsAsync("janus_maintenance"))
        {
            Assert.Equal(0, await maintenance.ExecuteScalarAsync<int>(
                "SELECT janus.audit_drop_expired_partitions(" + Retentions + ")"));
        }

        await using NpgsqlConnection application = await AsAsync("janus_app");

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await application.ExecuteScalarAsync<int>(
                "SELECT janus.audit_drop_expired_partitions(" + Retentions + ")"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
    }

    /// <summary>
    /// PRIV-RET-002 AC5, OPS-MIG-003a AC1: the maintenance role alters no schema of its
    /// own, so the one thing it can drop is what the function drops for it.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003a_AC1_TheMaintenanceRoleAltersNoSchemaAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("janus_maintenance");

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("DROP TABLE janus.accounts"));

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("ALTER TABLE janus.accounts ADD COLUMN spare integer"));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("CREATE TABLE janus.spare (id integer)"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
    }

    /// <summary>
    /// OPS-MIG-003a AC2: each maintenance function is created by a migration, owned by
    /// the role that ran it, and runs with that role's rights under a search path of
    /// its own, so nothing the caller sets decides what the function resolves.
    /// </summary>
    /// <param name="name">The function.</param>
    [Theory]
    [InlineData("audit_ensure_partitions")]
    [InlineData("audit_drop_expired_partitions")]
    public async Task OPS_MIG_003a_AC2_EachMaintenanceFunctionIsTheMigrationRolesOwnAsync(
        string name)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        (string owner, bool definer, string[] settings) =
            await connection.QuerySingleAsync<(string, bool, string[])>(
                """
                SELECT proowner::regrole::text, prosecdef, proconfig
                FROM pg_proc
                JOIN pg_namespace ON pg_namespace.oid = pg_proc.pronamespace
                WHERE nspname = 'janus' AND proname = @name
                """,
                new { name });

        Assert.Equal(
            await connection.ExecuteScalarAsync<string>("SELECT current_user"),
            owner);
        Assert.True(definer);
        Assert.Equal(["search_path=pg_catalog, pg_temp"], settings);
    }

    /// <summary>
    /// OPS-MIG-003a AC3: the application role executes no maintenance function, the
    /// sweep that creates the months no more than the drop that removes them.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003a_AC3_TheApplicationExecutesNoMaintenanceFunctionAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("janus_app");

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteScalarAsync<int>("SELECT janus.audit_ensure_partitions()"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
    }

    /// <summary>
    /// OPS-MIG-003a AC4: the maintenance role reads and updates the wrapped keys, which
    /// is what the re-wrap needs, and reaches no other table.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheKeysAndNoOtherTableAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("janus_maintenance");

        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM janus.subject_keys"));
        Assert.Equal(0, await connection.ExecuteAsync(
            "UPDATE janus.subject_keys SET key_version = key_version"));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM janus.accounts"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
    }

    /// <summary>
    /// OPS-MIG-003 AC1: the application role executes no schema-altering statement.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003_AC1_TheApplicationAltersNoSchemaAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("janus_app");

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("ALTER TABLE janus.accounts ADD COLUMN spare integer"));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("DROP TABLE janus.accounts"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
    }

    /// <summary>
    /// A retention below the floor its key carries is refused, so the caller cannot
    /// shorten retention by argument.
    /// </summary>
    /// <param name="arguments">The pair the caller passes.</param>
    [Theory]
    [InlineData("interval '4 years', interval '90 days'")]
    [InlineData("interval '7 years', interval '29 days'")]
    public async Task AuditDropExpiredPartitions_ARetentionBelowItsFloor_IsRefusedAsync(
        string arguments)
    {
        await using NpgsqlConnection connection = await AsAsync("janus_maintenance");

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteScalarAsync<int>(
                "SELECT janus.audit_drop_expired_partitions(" + arguments + ")"));
    }

    private static async Task<int> PartitionsAsync(NpgsqlConnection connection) =>
        await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*)::int
            FROM pg_class child
            JOIN pg_inherits ON pg_inherits.inhrelid = child.oid
            JOIN pg_class parent ON parent.oid = pg_inherits.inhparent
            WHERE child.relkind = 'r'
                AND parent.relname IN ('audit_records_security', 'audit_records_routine')
            """);

    private async Task<NpgsqlConnection> AsAsync(string role)
    {
        NpgsqlConnection connection = await database.OpenAsync();
        await connection.ExecuteAsync("SET ROLE " + role);

        return connection;
    }
}
