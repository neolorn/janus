using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddCallbackEventsAndReferences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "callback_events",
            schema: "identity",
            columns: table => new
            {
                callback = table.Column<string>(type: "text", nullable: false),
                identifier = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_callback_events", x => new { x.callback, x.identifier }));

        migrationBuilder.CreateTable(
            name: "callback_references",
            schema: "identity",
            columns: table => new
            {
                reference = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                callback = table.Column<string>(type: "text", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_callback_references", x => x.reference));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "callback_events",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "callback_references",
            schema: "identity");
    }
}
