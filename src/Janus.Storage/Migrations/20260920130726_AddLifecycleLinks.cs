using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddLifecycleLinks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lifecycle_links",
            schema: "janus",
            columns: table => new
            {
                token = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_lifecycle_links", x => x.token);
                table.CheckConstraint("ck_lifecycle_links_kind", "kind IN ('deletion-cancellation', 'reactivation')");
                table.CheckConstraint("ck_lifecycle_links_token", "octet_length(token) = 32");
                table.ForeignKey(
                    name: "fk_lifecycle_links_subject",
                    column: x => x.subject,
                    principalSchema: "janus",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_lifecycle_links_subject",
            schema: "janus",
            table: "lifecycle_links",
            column: "subject",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "lifecycle_links",
            schema: "janus");
    }
}
