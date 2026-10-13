using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class CountKeysApartFromDestinations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "send_key_counters",
            schema: "identity",
            columns: table => new
            {
                key = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                fingerprint_version = table.Column<int>(type: "integer", nullable: false),
                sent_at = table.Column<DateTimeOffset[]>(type: "timestamp with time zone[]", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_send_key_counters", x => x.key));

        // AUTH-ABUSE-004 AC6: the sweep deletes every record whose newest time decides
        // nothing, and the planner matches an index over an expression only as written,
        // so this is the statement the sweep generates, element for element, as for
        // send_counters.
        migrationBuilder.Sql(
            """
            CREATE INDEX ix_send_key_counters_last_sent_at
                ON identity.send_key_counters ((sent_at[(cardinality(sent_at) - 1) + 1]));
            """);

        // OPS-MIG-003: the application counts, releases and sweeps the keys.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.send_key_counters TO identity_app;
            """);

        // OPS-SEC-003: the fingerprint rotation reads a key's version and forgets what a
        // retired version counted, as it does for send_counters.
        migrationBuilder.Sql(
            """
            GRANT SELECT (fingerprint_version), DELETE ON identity.send_key_counters TO identity_maintenance;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "send_key_counters",
            schema: "identity");
    }
}
