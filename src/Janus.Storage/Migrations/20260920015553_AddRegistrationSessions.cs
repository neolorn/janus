using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddRegistrationSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "adult_affirmed",
            schema: "identity",
            table: "accounts",
            type: "boolean",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "age_group",
            schema: "identity",
            table: "accounts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "answered_age_at",
            schema: "identity",
            table: "accounts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "notice_version",
            schema: "identity",
            table: "accounts",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "terms_version",
            schema: "identity",
            table: "accounts",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "registration_sessions",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                provisional_subject = table.Column<Guid>(type: "uuid", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                key_version = table.Column<int>(type: "integer", nullable: false),
                wrapped_key = table.Column<byte[]>(type: "bytea", nullable: false),
                enc_session = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_registration_sessions", x => x.id));

        migrationBuilder.CreateTable(
            name: "registration_links",
            schema: "identity",
            columns: table => new
            {
                fingerprint = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                session = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_registration_links", x => x.fingerprint);
                table.ForeignKey(
                    name: "fk_registration_links_session",
                    column: x => x.session,
                    principalSchema: "identity",
                    principalTable: "registration_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_age_answer",
            schema: "identity",
            table: "accounts",
            sql: "adult_affirmed IS NULL OR age_group IS NULL");

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_age_group",
            schema: "identity",
            table: "accounts",
            sql: "age_group IS NULL OR age_group IN ('adult', 'minor')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_answered_age_at",
            schema: "identity",
            table: "accounts",
            sql: "(answered_age_at IS NULL) = (adult_affirmed IS NULL AND age_group IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_documents",
            schema: "identity",
            table: "accounts",
            sql: "(terms_version IS NULL) = (notice_version IS NULL)");

        migrationBuilder.CreateIndex(
            name: "ix_registration_links_session",
            schema: "identity",
            table: "registration_links",
            column: "session");

        migrationBuilder.CreateIndex(
            name: "ix_registration_sessions_expires_at",
            schema: "identity",
            table: "registration_sessions",
            column: "expires_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "registration_links",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "registration_sessions",
            schema: "identity");

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_age_answer",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_age_group",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_answered_age_at",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_documents",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "adult_affirmed",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "age_group",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "answered_age_at",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "notice_version",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "terms_version",
            schema: "identity",
            table: "accounts");
    }
}
