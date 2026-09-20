using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddSubjectEventOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "outbox",
            schema: "janus",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                restricted = table.Column<bool>(type: "boolean", nullable: false),
                reason = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox", x => x.id);
                table.CheckConstraint("ck_outbox_attempts", "attempts >= 0");
                table.CheckConstraint("ck_outbox_kind", "kind IN ('erasure-requested', 'export-requested', 'restriction-changed')");
                table.CheckConstraint("ck_outbox_reason", "reason IN ('erasure-request', 'minor-takedown', 'organization-erasure')");
                table.CheckConstraint("ck_outbox_status", "status IN ('awaiting-subscribers', 'complete', 'failed')");
                table.ForeignKey(
                    name: "fk_outbox_subject",
                    column: x => x.subject,
                    principalSchema: "janus",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "outbox_confirmations",
            schema: "janus",
            columns: table => new
            {
                delivery = table.Column<Guid>(type: "uuid", nullable: false),
                subscriber = table.Column<string>(type: "text", nullable: false),
                confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox_confirmations", x => new { x.delivery, x.subscriber });
                table.CheckConstraint("ck_outbox_confirmations_subscriber", "length(trim(subscriber)) > 0");
                table.ForeignKey(
                    name: "fk_outbox_confirmations_delivery",
                    column: x => x.delivery,
                    principalSchema: "janus",
                    principalTable: "outbox",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_outbox_due",
            schema: "janus",
            table: "outbox",
            column: "next_attempt_at",
            filter: "status = 'awaiting-subscribers'");

        migrationBuilder.CreateIndex(
            name: "ix_outbox_subject",
            schema: "janus",
            table: "outbox",
            column: "subject");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_confirmations",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "outbox",
            schema: "janus");
    }
}
