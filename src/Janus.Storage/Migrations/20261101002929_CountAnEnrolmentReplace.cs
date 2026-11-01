using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class CountAnEnrolmentReplace : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "replaced_at",
            schema: "identity",
            table: "recovery_links",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_recovery_links_replaced",
            schema: "identity",
            table: "recovery_links",
            sql: "replaced_at IS NULL OR session IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_recovery_links_replaced",
            schema: "identity",
            table: "recovery_links");

        migrationBuilder.DropColumn(
            name: "replaced_at",
            schema: "identity",
            table: "recovery_links");
    }
}
