using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddAdministrativeOrganization : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "administrative",
            schema: "janus",
            table: "organizations",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "ux_organizations_administrative",
            schema: "janus",
            table: "organizations",
            column: "administrative",
            unique: true,
            filter: "administrative");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_organizations_administrative",
            schema: "janus",
            table: "organizations");

        migrationBuilder.DropColumn(
            name: "administrative",
            schema: "janus",
            table: "organizations");
    }
}
