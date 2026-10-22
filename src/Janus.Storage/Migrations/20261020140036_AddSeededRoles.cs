using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddSeededRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "seeded_roles",
            schema: "identity",
            columns: table => new
            {
                name = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_seeded_roles", x => x.name));

        // OPS-MIG-003: the application's start step reads and keeps the seeded names.
        migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON identity.seeded_roles TO identity_app;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "seeded_roles",
            schema: "identity");
    }
}
