using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddIdentifierVerifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "identifier_verifications",
            schema: "identity",
            columns: table => new
            {
                identifier_id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                browser = table.Column<Guid>(type: "uuid", nullable: false),
                is_replacement = table.Column<bool>(type: "boolean", nullable: false),
                old_must_confirm = table.Column<bool>(type: "boolean", nullable: false),
                old_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                old_link = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                link = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                staged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                enc_staged = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_identifier_verifications", x => x.identifier_id);
                table.CheckConstraint("ck_identifier_verifications_link", "link IS NULL OR octet_length(link) = 32");
                table.CheckConstraint("ck_identifier_verifications_old", "is_replacement OR (NOT old_must_confirm AND old_confirmed_at IS NULL AND old_link IS NULL)");
                table.CheckConstraint("ck_identifier_verifications_old_link", "old_link IS NULL OR octet_length(old_link) = 32");
                table.ForeignKey(
                    name: "fk_identifier_verifications_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_identifier_verifications_staged_at",
            schema: "identity",
            table: "identifier_verifications",
            column: "staged_at");

        migrationBuilder.CreateIndex(
            name: "ix_identifier_verifications_subject",
            schema: "identity",
            table: "identifier_verifications",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ux_identifier_verifications_link",
            schema: "identity",
            table: "identifier_verifications",
            column: "link",
            unique: true,
            filter: "link IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_identifier_verifications_old_link",
            schema: "identity",
            table: "identifier_verifications",
            column: "old_link",
            unique: true,
            filter: "old_link IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "identifier_verifications",
            schema: "identity");
    }
}
