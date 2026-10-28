using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddRecordAccessSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<Guid>(
            name: "record_access",
            schema: "identity",
            table: "preauthentication_sessions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "record_access_sessions",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                fingerprints = table.Column<byte[]>(type: "bytea", nullable: false),
                throttled = table.Column<byte[]>(type: "bytea", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                reached = table.Column<Guid[]>(type: "uuid[]", nullable: false),
            },
            constraints: table => table.PrimaryKey("pk_record_access_sessions", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ux_preauthentication_sessions_record_access",
            schema: "identity",
            table: "preauthentication_sessions",
            column: "record_access",
            unique: true,
            filter: "record_access IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_record_access_sessions_expires_at",
            schema: "identity",
            table: "record_access_sessions",
            column: "expires_at");

        migrationBuilder.AddForeignKey(
            name: "fk_preauthentication_sessions_record_access",
            schema: "identity",
            table: "preauthentication_sessions",
            column: "record_access",
            principalSchema: "identity",
            principalTable: "record_access_sessions",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.record_access_sessions TO identity_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropForeignKey(
            name: "fk_preauthentication_sessions_record_access",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropTable(
            name: "record_access_sessions",
            schema: "identity");

        migrationBuilder.DropIndex(
            name: "ux_preauthentication_sessions_record_access",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropColumn(
            name: "record_access",
            schema: "identity",
            table: "preauthentication_sessions");
    }
}
