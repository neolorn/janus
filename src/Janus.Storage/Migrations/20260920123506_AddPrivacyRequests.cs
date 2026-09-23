using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddPrivacyRequests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "privacy_requests",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "text", nullable: false),
                detail = table.Column<string>(type: "text", nullable: false),
                received_at = table.Column<DateOnly>(type: "date", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                decision_due = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                warn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                escalate_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                decision_reason = table.Column<string>(type: "text", nullable: true),
                channel = table.Column<string>(type: "text", nullable: true),
                identity_confirmation = table.Column<string>(type: "text", nullable: true),
                warned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                escalated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_privacy_requests", x => x.id);
                table.CheckConstraint("ck_privacy_requests_status", "status IN ('deemed-refused-by-lapse', 'fulfilled', 'granted-by-lapse', 'open', 'refused')");
                table.CheckConstraint("ck_privacy_requests_type", "type IN ('erasure', 'rectification', 'restriction')");
                table.ForeignKey(
                    name: "fk_privacy_requests_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_privacy_requests_open",
            schema: "identity",
            table: "privacy_requests",
            column: "warn_at",
            filter: "status = 'open'");

        migrationBuilder.CreateIndex(
            name: "ix_privacy_requests_subject",
            schema: "identity",
            table: "privacy_requests",
            column: "subject");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "privacy_requests",
            schema: "identity");
    }
}
