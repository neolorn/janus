using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddErasures : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "erasures",
            schema: "janus",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                reason = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_erasures", x => x.subject);
                table.CheckConstraint("ck_erasures_attempts", "attempts >= 0");
                table.CheckConstraint("ck_erasures_reason", "reason IN ('erasure-request', 'minor-takedown', 'organization-erasure')");
                table.CheckConstraint("ck_erasures_status", "status IN ('awaiting-subscribers', 'complete', 'failed')");
                table.ForeignKey(
                    name: "fk_erasures_subject",
                    column: x => x.subject,
                    principalSchema: "janus",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_erasures_outstanding",
            schema: "janus",
            table: "erasures",
            column: "requested_at",
            filter: "status <> 'complete'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "erasures",
            schema: "janus");
    }
}
