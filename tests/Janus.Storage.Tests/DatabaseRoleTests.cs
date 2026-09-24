using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Model;
using Janus.Core;
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
        await using NpgsqlConnection connection = await AsAsync("identity_app");

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("UPDATE identity.audit_records SET action = 'a.b'"));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("DELETE FROM identity.audit_records"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM identity.audit_records"));
    }

    /// <summary>
    /// OPS-MAINT-001 AC3: an entry of the maintenance log cannot be changed or removed
    /// through the application, whose credential appends and reads and does no more.
    /// </summary>
    [Fact]
    public async Task OPS_MAINT_001_AC3_TheApplicationCannotChangeOrRemoveALogEntryAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("identity_app");

        PostgresException changed = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("UPDATE identity.maintenance_log SET note = 'altered'"));

        PostgresException removed = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("DELETE FROM identity.maintenance_log"));

        Assert.Equal(InsufficientPrivilege, changed.SqlState);
        Assert.Equal(InsufficientPrivilege, removed.SqlState);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM identity.maintenance_log"));
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
            CREATE TABLE identity.audit_records_routine_2020_01
                PARTITION OF identity.audit_records_routine
                FOR VALUES FROM ('2020-01-01 00:00:00+00') TO ('2020-02-01 00:00:00+00');
            """);

        int standing = await PartitionsAsync(connection);
        int dropped = await connection.ExecuteScalarAsync<int>(
            "SELECT identity.audit_drop_expired_partitions(" + Retentions + ")");

        Assert.Equal(1, dropped);
        Assert.Equal(standing - 1, await PartitionsAsync(connection));
        Assert.Null(await connection.ExecuteScalarAsync<string>(
            "SELECT to_regclass('identity.audit_records_routine_2020_01')::text"));
    }

    /// <summary>
    /// PRIV-RET-002 AC5: the maintenance role executes the drop, and the application
    /// role cannot execute it.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_002_AC5_OnlyTheMaintenanceRoleExecutesTheDropAsync()
    {
        await using (NpgsqlConnection maintenance = await AsAsync("identity_maintenance"))
        {
            Assert.Equal(0, await maintenance.ExecuteScalarAsync<int>(
                "SELECT identity.audit_drop_expired_partitions(" + Retentions + ")"));
        }

        await using NpgsqlConnection application = await AsAsync("identity_app");

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await application.ExecuteScalarAsync<int>(
                "SELECT identity.audit_drop_expired_partitions(" + Retentions + ")"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
    }

    /// <summary>
    /// PRIV-RET-002 AC5, OPS-MIG-003a AC1: the maintenance role alters no schema of its
    /// own, so the one thing it can drop is what the function drops for it.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003a_AC1_TheMaintenanceRoleAltersNoSchemaAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("identity_maintenance");

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("DROP TABLE identity.accounts"));

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("ALTER TABLE identity.accounts ADD COLUMN spare integer"));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("CREATE TABLE identity.spare (id integer)"));

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
                WHERE nspname = 'identity' AND proname = @name
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
        await using NpgsqlConnection connection = await AsAsync("identity_app");

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteScalarAsync<int>("SELECT identity.audit_ensure_partitions()"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
    }

    /// <summary>
    /// OPS-MIG-003a AC4: the maintenance role reads and updates the wrapped keys, which
    /// is what the re-wrap needs, and reaches no other table.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheKeysAndNoOtherTableAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("identity_maintenance");

        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM identity.subject_keys"));
        Assert.Equal(0, await connection.ExecuteAsync(
            "UPDATE identity.subject_keys SET key_version = key_version"));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM identity.accounts"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
    }

    /// <summary>
    /// OPS-MIG-003a AC4: the maintenance role reads and writes the rotation's progress,
    /// and of a table holding a value wrapped beside the subject keys it reaches the
    /// row's key, the version and the wrapped value and no other column (entry 316 of
    /// the decisions pending review).
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheWrappedValuesAndNoOtherColumnAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("identity_maintenance");

        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM identity.key_rotations"));
        Assert.Equal(0, await connection.ExecuteAsync(
            "UPDATE identity.key_rotations SET processed = processed"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT count(key_version)::int FROM identity.invitations"));
        Assert.Equal(0, await connection.ExecuteAsync(
            "UPDATE identity.signing_keys SET key_version = key_version WHERE key_id = key_id"));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(enc_identifiers)::int FROM identity.invitations"));

        Assert.Equal(InsufficientPrivilege, refused.SqlState);

        await using NpgsqlConnection application = await AsAsync("identity_app");

        PostgresException withheld = await Assert.ThrowsAsync<PostgresException>(async () =>
            await application.ExecuteScalarAsync<int>("SELECT count(*)::int FROM identity.key_rotations"));

        Assert.Equal(InsufficientPrivilege, withheld.SqlState);
    }

    /// <summary>
    /// OPS-MIG-003a AC4, OPS-SEC-003 AC6: of a table holding a keyed fingerprint the
    /// maintenance role reaches what computing it again needs and no other column, and
    /// of a ledger the version a line is hashed under and the line to forget, never the
    /// hash (entry 318 of the decisions pending review).
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheFingerprintsAndNoOtherColumnAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("identity_maintenance");

        Assert.Equal(0, await connection.ExecuteAsync(
            """
            UPDATE identity.identifiers SET fingerprint = fingerprint, fingerprint_version = fingerprint_version
            WHERE identifier_id = identifier_id AND subject = subject AND enc_canonical = enc_canonical
            """));
        Assert.Equal(0, await connection.ExecuteAsync(
            "DELETE FROM identity.throttle_counters WHERE fingerprint_version <> 1"));

        PostgresException withheld = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteScalarAsync<int>("SELECT count(enc_entered)::int FROM identity.identifiers"));
        PostgresException hashed = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteScalarAsync<int>("SELECT count(key)::int FROM identity.throttle_counters"));

        Assert.Equal(InsufficientPrivilege, withheld.SqlState);
        Assert.Equal(InsufficientPrivilege, hashed.SqlState);
    }

    /// <summary>
    /// OPS-MIG-003a AC2, AC4: what the serialized model lists for the maintenance
    /// credential is what the database grants it, so the listing a reviewer reads
    /// cannot drift from the migration that writes the grants.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003a_AC4_TheListedGrantsAreTheOnesTheDatabaseHoldsAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> held = await connection.QueryAsync<string>(
            """
            SELECT 'SCHEMA ' || nspname || ' ' || right_held
            FROM pg_namespace,
                 unnest(ARRAY['USAGE', 'CREATE']) AS right_held
            WHERE nspname = 'identity'
              AND has_schema_privilege('identity_maintenance', oid, right_held)
            UNION ALL
            SELECT 'TABLE identity.' || relname || ' ' || right_held
            FROM pg_class
            JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace,
                 unnest(ARRAY['SELECT', 'INSERT', 'UPDATE', 'DELETE']) AS right_held
            WHERE nspname = 'identity'
              AND relkind IN ('r', 'p')
              AND has_table_privilege('identity_maintenance', pg_class.oid, right_held)
            UNION ALL
            SELECT 'COLUMN identity.' || relname || '.' || attname || ' ' || right_held
            FROM pg_attribute
            JOIN pg_class ON pg_class.oid = pg_attribute.attrelid
            JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace,
                 unnest(ARRAY['SELECT', 'INSERT', 'UPDATE']) AS right_held
            WHERE nspname = 'identity'
              AND relkind IN ('r', 'p')
              AND attnum > 0
              AND NOT attisdropped
              AND has_column_privilege('identity_maintenance', pg_class.oid, attnum, right_held)
              AND NOT has_table_privilege('identity_maintenance', pg_class.oid, right_held)
            UNION ALL
            SELECT 'FUNCTION identity.' || proname || '('
                   || pg_get_function_identity_arguments(pg_proc.oid) || ') EXECUTE'
            FROM pg_proc
            JOIN pg_namespace ON pg_namespace.oid = pg_proc.pronamespace
            WHERE nspname = 'identity'
              AND has_function_privilege('identity_maintenance', pg_proc.oid, 'EXECUTE')
            """);

        Assert.Equal(Listed().Order(StringComparer.Ordinal), held.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// OPS-MIG-003: the application reaches the rows of every table the library holds,
    /// the audit trail (PRIV-RET-002) and the maintenance log (OPS-MAINT-001) only to
    /// read and append, the migration history and the views only to read, and every
    /// sequence it draws from. A table a migration adds without granting it fails here
    /// rather than under the application's own credential. The key rotation's progress is the maintenance credential's alone
    /// (OPS-MIG-003a AC4).
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003_TheApplicationReachesTheRowsOfEveryTableAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<string> withheld = await connection.QueryAsync<string>(
            """
            SELECT relname || ' ' || right_held
            FROM pg_class
            JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace,
                 unnest(CASE
                     WHEN relkind = 'v' OR relname = '__migrations_history' THEN ARRAY['SELECT']
                     WHEN relname IN ('audit_records', 'maintenance_log') THEN ARRAY['SELECT', 'INSERT']
                     WHEN relkind = 'S' THEN ARRAY['USAGE']
                     ELSE ARRAY['SELECT', 'INSERT', 'UPDATE', 'DELETE'] END) AS right_held
            WHERE nspname = 'identity'
              AND relkind IN ('r', 'p', 'v', 'S')
              AND NOT relispartition
              AND relname <> 'key_rotations'
              AND NOT CASE relkind
                  WHEN 'S' THEN has_sequence_privilege('identity_app', pg_class.oid, right_held)
                  ELSE has_table_privilege('identity_app', pg_class.oid, right_held) END
            ORDER BY 1
            """);

        Assert.Empty(withheld);
    }

    /// <summary>
    /// OPS-MIG-003 AC1: the application role executes no schema-altering statement.
    /// </summary>
    [Fact]
    public async Task OPS_MIG_003_AC1_TheApplicationAltersNoSchemaAsync()
    {
        await using NpgsqlConnection connection = await AsAsync("identity_app");

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("ALTER TABLE identity.accounts ADD COLUMN spare integer"));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("DROP TABLE identity.accounts"));

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
        await using NpgsqlConnection connection = await AsAsync("identity_maintenance");

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteScalarAsync<int>(
                "SELECT identity.audit_drop_expired_partitions(" + arguments + ")"));
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

    private static List<string> Listed()
    {
        using var written = JsonDocument.Parse(
            AuthorizationModel.Of(new AuthorizationDeclarationBuilder().Build()).Serialize());

        var listed = new List<string>();

        foreach (JsonElement grant in
            written.RootElement.GetProperty("maintenanceGrants").EnumerateArray())
        {
            listed.Add(
                grant.GetProperty("on").GetString()
                + " "
                + grant.GetProperty("right").GetString());
        }

        return listed;
    }

    private async Task<NpgsqlConnection> AsAsync(string role)
    {
        NpgsqlConnection connection = await database.OpenAsync();
        await connection.ExecuteAsync("SET ROLE " + role);

        return connection;
    }
}
