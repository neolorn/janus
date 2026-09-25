using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class HoldRestrictions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<bool>(
            name: "restriction_held",
            schema: "identity",
            table: "accounts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_restriction_held",
            schema: "identity",
            table: "accounts",
            sql: "NOT restriction_held OR state IN ('deleting', 'suspended')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_restriction_held",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "restriction_held",
            schema: "identity",
            table: "accounts");
    }
}
