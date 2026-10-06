using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeepWhenAnEnrolmentSessionShowedRecoveryCodes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTH-RECOV-006 (D-189): when a second step enrolled in the enrolment session a
        // spent link stands for showed recovery codes; nothing where none did. A link
        // that opened no session shows none.
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "codes_shown_at",
            schema: "identity",
            table: "recovery_links",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_recovery_links_codes_shown",
            schema: "identity",
            table: "recovery_links",
            sql: "codes_shown_at IS NULL OR session IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_recovery_links_codes_shown",
            schema: "identity",
            table: "recovery_links");

        migrationBuilder.DropColumn(
            name: "codes_shown_at",
            schema: "identity",
            table: "recovery_links");
    }
}
