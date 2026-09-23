using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddSendOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "send_outbox",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: true),
                key_version = table.Column<int>(type: "integer", nullable: false),
                wrapped_key = table.Column<byte[]>(type: "bytea", nullable: false),
                enc_message = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_send_outbox", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_send_outbox_recorded_at",
            schema: "identity",
            table: "send_outbox",
            column: "recorded_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "send_outbox",
            schema: "identity");
    }
}
