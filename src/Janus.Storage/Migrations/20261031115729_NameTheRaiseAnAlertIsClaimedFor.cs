using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class NameTheRaiseAnAlertIsClaimedFor : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<Guid>(
            name: "raise",
            schema: "identity",
            table: "alerts",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "unanswered",
            schema: "identity",
            table: "alerts",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "raise",
            schema: "identity",
            table: "alerts");

        migrationBuilder.DropColumn(
            name: "unanswered",
            schema: "identity",
            table: "alerts");
    }
}
