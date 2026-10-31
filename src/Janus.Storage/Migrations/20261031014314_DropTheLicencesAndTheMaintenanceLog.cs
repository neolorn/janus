using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class DropTheLicencesAndTheMaintenanceLog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // D-225: the deployment keeps its own licences and maintenance log, so the library
        // holds neither.
        migrationBuilder.DropTable(
            name: "licences",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "maintenance_log",
            schema: "identity");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

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
            constraints: table =>
            {
                table.PrimaryKey("pk_maintenance_log", x => x.id);
                table.CheckConstraint("ck_maintenance_log_actor_not_max_uuid", "actor <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");
            });
    }
}
