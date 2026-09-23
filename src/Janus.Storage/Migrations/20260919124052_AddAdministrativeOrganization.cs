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
            schema: "identity",
            table: "organizations",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "ux_organizations_administrative",
            schema: "identity",
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
            schema: "identity",
            table: "organizations");

        migrationBuilder.DropColumn(
            name: "administrative",
            schema: "identity",
            table: "organizations");
    }
}
