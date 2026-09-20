using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddSending : Migration
{
    private static readonly string[] SourceAndAt = ["source", "at"];
    private static readonly string[] DestinationAndAt = ["destination", "at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "alerts",
            schema: "janus",
            columns: table => new
            {
                key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_alerts", x => x.key));

        migrationBuilder.CreateTable(
            name: "callbacks",
            schema: "janus",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                source = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                rejected = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_callbacks", x => x.id));

        migrationBuilder.CreateTable(
            name: "nonexistence_notices",
            schema: "janus",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                destination = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_nonexistence_notices", x => x.id));

        migrationBuilder.CreateTable(
            name: "registration_sources",
            schema: "janus",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                source = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_registration_sources", x => x.id));

        migrationBuilder.CreateTable(
            name: "send_counters",
            schema: "janus",
            columns: table => new
            {
                key = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                sent_at = table.Column<DateTimeOffset[]>(type: "timestamp with time zone[]", nullable: false),
                settles_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_send_counters", x => x.key));

        migrationBuilder.CreateTable(
            name: "send_grants",
            schema: "janus",
            columns: table => new
            {
                key = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                credit = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_send_grants", x => x.key);
                table.CheckConstraint("ck_send_grants_credit", "credit > 0");
            });

        migrationBuilder.CreateTable(
            name: "sends",
            schema: "janus",
            columns: table => new
            {
                reference = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                counted = table.Column<byte[][]>(type: "bytea[]", nullable: false),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                settles_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_sends", x => x.reference));

        migrationBuilder.CreateTable(
            name: "sms_balance_readings",
            schema: "janus",
            columns: table => new
            {
                read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                balance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_sms_balance_readings", x => x.read_at));

        migrationBuilder.CreateTable(
            name: "throttle_counters",
            schema: "janus",
            columns: table => new
            {
                scope = table.Column<string>(type: "text", nullable: false),
                key = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                failures = table.Column<int>(type: "integer", nullable: false),
                at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_throttle_counters", x => new { x.scope, x.key });
                table.CheckConstraint("ck_throttle_counters_scope", "scope IN ('account', 'identifier', 'source')");
            });

        migrationBuilder.CreateIndex(
            name: "ix_callbacks_source_at",
            schema: "janus",
            table: "callbacks",
            columns: SourceAndAt);

        migrationBuilder.CreateIndex(
            name: "ix_nonexistence_notices_destination_at",
            schema: "janus",
            table: "nonexistence_notices",
            columns: DestinationAndAt);

        migrationBuilder.CreateIndex(
            name: "ix_registration_sources_source_at",
            schema: "janus",
            table: "registration_sources",
            columns: SourceAndAt);

        migrationBuilder.CreateIndex(
            name: "ix_send_counters_settles_at",
            schema: "janus",
            table: "send_counters",
            column: "settles_at");

        migrationBuilder.CreateIndex(
            name: "ix_sends_settles_at",
            schema: "janus",
            table: "sends",
            column: "settles_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "alerts",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "callbacks",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "nonexistence_notices",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "registration_sources",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "send_counters",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "send_grants",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "sends",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "sms_balance_readings",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "throttle_counters",
            schema: "janus");
    }
}
