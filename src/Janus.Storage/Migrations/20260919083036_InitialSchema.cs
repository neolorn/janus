using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class InitialSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "identity");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:CollationDefinition:identity.identity_ci", "und-u-ks-level2,und-u-ks-level2,icu,False");

        migrationBuilder.CreateTable(
            name: "accounts",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                state = table.Column<string>(type: "text", nullable: false),
                suspended_by = table.Column<string>(type: "text", nullable: true),
                deleting_by = table.Column<string>(type: "text", nullable: true),
                deleting_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_accounts", x => x.subject);
                table.CheckConstraint("ck_accounts_deleting", "(deleting_by IS NULL) = (deleting_since IS NULL)");
                table.CheckConstraint("ck_accounts_deleting_by", "deleting_by IS NULL OR deleting_by IN ('oob-request', 'self', 'takedown')");
                table.CheckConstraint("ck_accounts_state", "state IN ('active', 'deleted', 'deleting', 'restricted', 'suspended')");
                table.CheckConstraint("ck_accounts_suspended_by", "suspended_by IS NULL OR suspended_by IN ('administrator', 'self')");
            });

        migrationBuilder.CreateTable(
            name: "subject_keys",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                format_marker = table.Column<byte>(type: "smallint", nullable: false),
                key_version = table.Column<int>(type: "integer", nullable: false),
                wrapped_key = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_subject_keys", x => x.subject);
                table.CheckConstraint("ck_subject_keys_format", "(format_marker = 1 AND octet_length(wrapped_key) = 40) OR (format_marker = 0 AND wrapped_key = decode(repeat('00', 32), 'hex'))");
                table.CheckConstraint("ck_subject_keys_version", "key_version >= 1");
            });

        migrationBuilder.CreateIndex(
            name: "ix_accounts_deleting_since",
            schema: "identity",
            table: "accounts",
            column: "deleting_since",
            filter: "deleting_since IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_subject_keys_key_version",
            schema: "identity",
            table: "subject_keys",
            column: "key_version");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "accounts",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "subject_keys",
            schema: "identity");
    }
}
