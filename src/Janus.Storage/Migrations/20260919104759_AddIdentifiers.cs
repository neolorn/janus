using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddIdentifiers : Migration
{
    private static readonly string[] SubjectAndKind = ["subject", "kind"];
    private static readonly string[] KindAndFingerprint = ["kind", "fingerprint"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "identifiers",
            schema: "identity",
            columns: table => new
            {
                identifier_id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                canonicalisation_version = table.Column<string>(type: "text", nullable: false),
                enc_entered = table.Column<byte[]>(type: "bytea", nullable: false),
                enc_canonical = table.Column<byte[]>(type: "bytea", nullable: false),
                added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                is_primary = table.Column<bool>(type: "boolean", nullable: false),
                is_locked = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_identifiers", x => x.identifier_id);
                table.CheckConstraint("ck_identifiers_fingerprint", "octet_length(fingerprint) = 32");
                table.CheckConstraint("ck_identifiers_kind", "kind IN ('email', 'phone', 'username')");
                table.CheckConstraint("ck_identifiers_primary", "NOT is_primary OR verified_at IS NOT NULL");
                table.ForeignKey(
                    name: "fk_identifiers_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "identifier_backup_settings",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                rule = table.Column<string>(type: "text", nullable: true),
                named = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_identifier_backup_settings", x => new { x.subject, x.kind });
                table.CheckConstraint("ck_identifier_backup_settings_kind", "kind IN ('email', 'phone', 'username')");
                table.CheckConstraint("ck_identifier_backup_settings_rule", "rule IN ('all-verified', 'primary-only') OR rule IS NULL");
                table.CheckConstraint("ck_identifier_backup_settings_setting", "(rule IS NULL) <> (named IS NULL)");
                table.ForeignKey(
                    name: "fk_identifier_backup_settings_named",
                    column: x => x.named,
                    principalSchema: "identity",
                    principalTable: "identifiers",
                    principalColumn: "identifier_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_identifier_backup_settings_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_identifier_backup_settings_named",
            schema: "identity",
            table: "identifier_backup_settings",
            column: "named");

        migrationBuilder.CreateIndex(
            name: "ix_identifiers_subject",
            schema: "identity",
            table: "identifiers",
            columns: SubjectAndKind);

        migrationBuilder.CreateIndex(
            name: "ux_identifiers_fingerprint",
            schema: "identity",
            table: "identifiers",
            columns: KindAndFingerprint,
            unique: true,
            filter: "fingerprint <> decode(repeat('00', 32), 'hex')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "identifier_backup_settings",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "identifiers",
            schema: "identity");
    }
}
