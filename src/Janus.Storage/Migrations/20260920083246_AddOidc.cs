using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddOidc : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "oidc_clients",
            schema: "identity",
            columns: table => new
            {
                client_id = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                redirect = table.Column<string>(type: "text", nullable: false),
                secret = table.Column<byte[]>(type: "bytea", nullable: false),
                scopes = table.Column<string[]>(type: "text[]", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_oidc_clients", x => x.client_id);
                table.CheckConstraint("ck_oidc_clients_kind", "kind IN ('browser-application', 'protocol')");
            });

        migrationBuilder.CreateTable(
            name: "signing_keys",
            schema: "identity",
            columns: table => new
            {
                key_id = table.Column<string>(type: "text", nullable: false),
                algorithm = table.Column<string>(type: "text", nullable: false),
                public_key = table.Column<byte[]>(type: "bytea", nullable: false),
                private_key = table.Column<byte[]>(type: "bytea", nullable: false),
                key_version = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                retires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_signing_keys", x => x.key_id);
                table.CheckConstraint("ck_signing_keys_retirement", "(superseded_at IS NULL AND retires_at IS NULL) OR (superseded_at IS NOT NULL AND retires_at > superseded_at)");
                table.CheckConstraint("ck_signing_keys_version", "key_version >= 1");
            });

        migrationBuilder.CreateTable(
            name: "oidc_codes",
            schema: "identity",
            columns: table => new
            {
                fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                client_id = table.Column<string>(type: "text", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                session = table.Column<Guid>(type: "uuid", nullable: false),
                redirect = table.Column<string>(type: "text", nullable: false),
                challenge = table.Column<string>(type: "text", nullable: false),
                challenge_method = table.Column<string>(type: "text", nullable: false),
                scope = table.Column<string>(type: "text", nullable: false),
                nonce = table.Column<string>(type: "text", nullable: true),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                spent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_oidc_codes", x => x.fingerprint);
                table.CheckConstraint("ck_oidc_codes_expiry", "expires_at > issued_at");
                table.CheckConstraint("ck_oidc_codes_method", "challenge_method = 'S256'");
                table.ForeignKey(
                    name: "fk_oidc_codes_client_id",
                    column: x => x.client_id,
                    principalSchema: "identity",
                    principalTable: "oidc_clients",
                    principalColumn: "client_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_oidc_codes_session",
                    column: x => x.session,
                    principalSchema: "identity",
                    principalTable: "sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_oidc_codes_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "oidc_refresh_tokens",
            schema: "identity",
            columns: table => new
            {
                fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                family = table.Column<Guid>(type: "uuid", nullable: false),
                client_id = table.Column<string>(type: "text", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                session = table.Column<Guid>(type: "uuid", nullable: false),
                scope = table.Column<string>(type: "text", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_oidc_refresh_tokens", x => x.fingerprint);
                table.CheckConstraint("ck_oidc_refresh_tokens_expiry", "expires_at > issued_at");
                table.ForeignKey(
                    name: "fk_oidc_refresh_tokens_client_id",
                    column: x => x.client_id,
                    principalSchema: "identity",
                    principalTable: "oidc_clients",
                    principalColumn: "client_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_oidc_refresh_tokens_session",
                    column: x => x.session,
                    principalSchema: "identity",
                    principalTable: "sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_oidc_refresh_tokens_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_oidc_codes_client_id",
            schema: "identity",
            table: "oidc_codes",
            column: "client_id");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_codes_expires_at",
            schema: "identity",
            table: "oidc_codes",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_codes_session",
            schema: "identity",
            table: "oidc_codes",
            column: "session");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_codes_subject",
            schema: "identity",
            table: "oidc_codes",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_refresh_tokens_client_id",
            schema: "identity",
            table: "oidc_refresh_tokens",
            column: "client_id");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_refresh_tokens_expires_at",
            schema: "identity",
            table: "oidc_refresh_tokens",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_refresh_tokens_family",
            schema: "identity",
            table: "oidc_refresh_tokens",
            column: "family");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_refresh_tokens_session",
            schema: "identity",
            table: "oidc_refresh_tokens",
            column: "session");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_refresh_tokens_subject",
            schema: "identity",
            table: "oidc_refresh_tokens",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ix_signing_keys_key_version",
            schema: "identity",
            table: "signing_keys",
            column: "key_version");

        migrationBuilder.CreateIndex(
            name: "ix_signing_keys_retires_at",
            schema: "identity",
            table: "signing_keys",
            column: "retires_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "oidc_codes",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "oidc_refresh_tokens",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "signing_keys",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "oidc_clients",
            schema: "identity");
    }
}
