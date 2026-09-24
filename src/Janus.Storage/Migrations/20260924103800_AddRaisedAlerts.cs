using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddRaisedAlerts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "raised_alerts",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                idempotency_key = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                condition = table.Column<string>(type: "text", nullable: false),
                details = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_raised_alerts", x => x.id));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "raised_alerts",
            schema: "identity");
    }
}
