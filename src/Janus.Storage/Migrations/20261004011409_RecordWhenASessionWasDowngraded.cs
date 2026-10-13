using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class RecordWhenASessionWasDowngraded : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTH-SESS-001, AUTH-SESS-009 (D-183): the session record holds the instant it
        // was last downgraded, and none where it never was. No existing row is
        // rewritten.
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "downgraded_at",
            schema: "identity",
            table: "sessions",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "downgraded_at",
            schema: "identity",
            table: "sessions");
    }
}
