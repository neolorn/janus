using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddReadVolume : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "read_baselines",
            schema: "identity",
            columns: table => new
            {
                actor = table.Column<Guid>(type: "uuid", nullable: false),
                daily_mean = table.Column<decimal>(type: "numeric", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_read_baselines", x => x.actor));

        migrationBuilder.CreateTable(
            name: "read_volume",
            schema: "identity",
            columns: table => new
            {
                actor = table.Column<Guid>(type: "uuid", nullable: false),
                day = table.Column<DateOnly>(type: "date", nullable: false),
                records = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_read_volume", x => new { x.actor, x.day }));

        migrationBuilder.CreateIndex(
            name: "ix_read_volume_day",
            schema: "identity",
            table: "read_volume",
            column: "day");

        // OPS-MIG-003: the application adds to the day's counts, and the job it runs
        // recomputes the means and forgets the counts older than the window.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.read_volume TO identity_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.read_baselines TO identity_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "read_baselines",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "read_volume",
            schema: "identity");
    }
}
