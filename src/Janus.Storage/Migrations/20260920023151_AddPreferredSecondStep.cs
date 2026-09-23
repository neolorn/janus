using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddPreferredSecondStep : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_preferred",
            schema: "identity",
            table: "authenticators",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "ux_authenticators_preferred",
            schema: "identity",
            table: "authenticators",
            column: "subject",
            unique: true,
            filter: "is_preferred");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_authenticators_preferred",
            schema: "identity",
            table: "authenticators");

        migrationBuilder.DropColumn(
            name: "is_preferred",
            schema: "identity",
            table: "authenticators");
    }
}
