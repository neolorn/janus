using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddRoleHandsOut : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTHZ-GRANT-005 (D-212): a role stood before the list did hands out none.
        migrationBuilder.AddColumn<string[]>(
            name: "hands_out",
            schema: "identity",
            table: "roles",
            type: "text[]",
            nullable: false,
            defaultValueSql: "'{}'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "hands_out",
            schema: "identity",
            table: "roles");
    }
}
