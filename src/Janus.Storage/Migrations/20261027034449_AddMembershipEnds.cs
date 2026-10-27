using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddMembershipEnds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // IDN-MEM-001 (D-213): a membership stood before an end could be set has none.
        migrationBuilder.AddColumn<string>(
            name: "end_reason",
            schema: "identity",
            table: "memberships",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "end_set_by",
            schema: "identity",
            table: "memberships",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ends_at",
            schema: "identity",
            table: "memberships",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_memberships_end_set",
            schema: "identity",
            table: "memberships",
            sql: "(ends_at IS NULL AND end_set_by IS NULL AND end_reason IS NULL)\nOR (ends_at IS NOT NULL AND end_set_by IS NOT NULL\n    AND length(btrim(end_reason)) BETWEEN 1 AND 1024)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_memberships_end_set",
            schema: "identity",
            table: "memberships");

        migrationBuilder.DropColumn(
            name: "end_reason",
            schema: "identity",
            table: "memberships");

        migrationBuilder.DropColumn(
            name: "end_set_by",
            schema: "identity",
            table: "memberships");

        migrationBuilder.DropColumn(
            name: "ends_at",
            schema: "identity",
            table: "memberships");
    }
}
