using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class HoldTheStateATakedownFinds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // IDN-LIFE-003: a takedown holds the suspension or the deletion it found, so its
        // reversal restores it; every account standing today holds neither.
        migrationBuilder.AddColumn<string>(
            name: "suspension_held",
            schema: "identity",
            table: "accounts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "deletion_held",
            schema: "identity",
            table: "accounts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "deletion_held_since",
            schema: "identity",
            table: "accounts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_suspension_held",
            schema: "identity",
            table: "accounts",
            sql: "suspension_held IS NULL OR suspension_held IN ('administrator', 'self')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_suspension_held_state",
            schema: "identity",
            table: "accounts",
            sql: "suspension_held IS NULL OR (state = 'deleting' AND deleting_by IN ('takedown', 'oob-request'))");

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_deletion_held",
            schema: "identity",
            table: "accounts",
            sql: "deletion_held IS NULL OR deletion_held IN ('oob-request', 'self')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_deletion_held_state",
            schema: "identity",
            table: "accounts",
            sql: "deletion_held IS NULL OR (state = 'deleting' AND deleting_by = 'takedown')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_deletion_held_since",
            schema: "identity",
            table: "accounts",
            sql: "(deletion_held IS NULL) = (deletion_held_since IS NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_deletion_held_since",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_deletion_held_state",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_deletion_held",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_suspension_held_state",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_suspension_held",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "deletion_held_since",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "deletion_held",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "suspension_held",
            schema: "identity",
            table: "accounts");
    }
}
