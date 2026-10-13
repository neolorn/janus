using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class ClaimARaisedAlertBeforeItIsCarried : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // CONV-DESIGN-003, INF-BG-001 (D-183): a raised condition is claimed before the
        // router is called, until this instant; nothing where no pass holds it.
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "claimed_until",
            schema: "identity",
            table: "raised_alerts",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "claimed_until",
            schema: "identity",
            table: "raised_alerts");
    }
}
