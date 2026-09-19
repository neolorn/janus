using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "settings",
            schema: "janus",
            columns: table => new
            {
                key = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_settings", x => x.key));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "settings",
            schema: "janus");
    }
}
