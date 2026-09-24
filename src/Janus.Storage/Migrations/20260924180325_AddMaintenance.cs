using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddMaintenance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "licences",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                renewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_licences", x => x.id));

        migrationBuilder.CreateTable(
            name: "maintenance_log",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                task = table.Column<string>(type: "text", nullable: false),
                performed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                actor = table.Column<Guid>(type: "uuid", nullable: false),
                note = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_maintenance_log", x => x.id));

        // OPS-MIG-003: the application edits the licences and appends to the log.
        // OPS-MAINT-001 AC3: no entry of the log can be changed or removed through the
        // application, so its credential holds no right that would.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.licences TO identity_app;
            GRANT SELECT, INSERT ON identity.maintenance_log TO identity_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "licences",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "maintenance_log",
            schema: "identity");
    }
}
