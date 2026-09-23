using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeepPersonalEmailsThroughMemberships : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<bool>(
            name: "is_personal",
            schema: "identity",
            table: "identifiers",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddCheckConstraint(
            name: "ck_identifiers_personal",
            schema: "identity",
            table: "identifiers",
            sql: "NOT is_personal OR (kind = 'email' AND verified_at IS NOT NULL AND NOT is_primary)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_identifiers_personal",
            schema: "identity",
            table: "identifiers");

        migrationBuilder.DropColumn(
            name: "is_personal",
            schema: "identity",
            table: "identifiers");
    }
}
