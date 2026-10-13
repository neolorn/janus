using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeepTheTransportsOfAKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTH-FACT-014 (D-197): a key enrolled before this runs holds no transports,
        // which is what a creation that gave none leaves.
        migrationBuilder.AddColumn<string[]>(
            name: "transports",
            schema: "identity",
            table: "authenticators",
            type: "text[]",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_authenticators_transports",
            schema: "identity",
            table: "authenticators",
            sql: "(transports IS NULL) OR (credential_id IS NOT NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_authenticators_transports",
            schema: "identity",
            table: "authenticators");

        migrationBuilder.DropColumn(
            name: "transports",
            schema: "identity",
            table: "authenticators");
    }
}
