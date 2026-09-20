using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddIdentifierRemovals : Migration
{
    private static readonly string[] KindAndFingerprint = ["kind", "fingerprint"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "identifier_removals",
            schema: "janus",
            columns: table => new
            {
                identifier_id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                enc_entered = table.Column<byte[]>(type: "bytea", nullable: false),
                enc_canonical = table.Column<byte[]>(type: "bytea", nullable: false),
                is_locked = table.Column<bool>(type: "boolean", nullable: false),
                added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                undo_fingerprint = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_identifier_removals", x => x.identifier_id);
                table.CheckConstraint("ck_identifier_removals_fingerprint", "octet_length(fingerprint) = 32");
                table.CheckConstraint("ck_identifier_removals_kind", "kind IN ('email', 'phone', 'username')");
                table.CheckConstraint("ck_identifier_removals_window", "expires_at > removed_at");
                table.ForeignKey(
                    name: "fk_identifier_removals_subject",
                    column: x => x.subject,
                    principalSchema: "janus",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "username_holds",
            schema: "janus",
            columns: table => new
            {
                fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                held_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                releases_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_username_holds", x => x.fingerprint);
                table.CheckConstraint("ck_username_holds_fingerprint", "octet_length(fingerprint) = 32");
            });

        migrationBuilder.CreateIndex(
            name: "ix_identifier_removals_expires_at",
            schema: "janus",
            table: "identifier_removals",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_identifier_removals_subject",
            schema: "janus",
            table: "identifier_removals",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ux_identifier_removals_fingerprint",
            schema: "janus",
            table: "identifier_removals",
            columns: KindAndFingerprint,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_identifier_removals_undo",
            schema: "janus",
            table: "identifier_removals",
            column: "undo_fingerprint",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_username_holds_releases_at",
            schema: "janus",
            table: "username_holds",
            column: "releases_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "identifier_removals",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "username_holds",
            schema: "janus");
    }
}
