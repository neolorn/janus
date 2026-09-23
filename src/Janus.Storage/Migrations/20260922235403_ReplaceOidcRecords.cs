using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class ReplaceOidcRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "oidc_codes",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "oidc_refresh_tokens",
            schema: "identity");

        migrationBuilder.CreateTable(
            name: "oidc_authorizations",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                application_id = table.Column<string>(type: "text", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                type = table.Column<string>(type: "text", nullable: false),
                scopes = table.Column<string[]>(type: "text[]", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                properties = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_oidc_authorizations", x => x.id);
                table.ForeignKey(
                    name: "fk_oidc_authorizations_application_id",
                    column: x => x.application_id,
                    principalSchema: "identity",
                    principalTable: "oidc_clients",
                    principalColumn: "client_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_oidc_authorizations_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "oidc_scopes",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                display_name = table.Column<string>(type: "text", nullable: true),
                display_names = table.Column<string>(type: "jsonb", nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                descriptions = table.Column<string>(type: "jsonb", nullable: false),
                resources = table.Column<string[]>(type: "text[]", nullable: false),
                properties = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_oidc_scopes", x => x.id));

        migrationBuilder.CreateTable(
            name: "oidc_tokens",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                application_id = table.Column<string>(type: "text", nullable: false),
                authorization_id = table.Column<Guid>(type: "uuid", nullable: true),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                type = table.Column<string>(type: "text", nullable: false),
                reference_id = table.Column<string>(type: "text", nullable: true),
                payload = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                redeemed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                properties = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_oidc_tokens", x => x.id);
                table.ForeignKey(
                    name: "fk_oidc_tokens_application_id",
                    column: x => x.application_id,
                    principalSchema: "identity",
                    principalTable: "oidc_clients",
                    principalColumn: "client_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_oidc_tokens_authorization_id",
                    column: x => x.authorization_id,
                    principalSchema: "identity",
                    principalTable: "oidc_authorizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_oidc_tokens_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_oidc_authorizations_application_id",
            schema: "identity",
            table: "oidc_authorizations",
            column: "application_id");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_authorizations_created_at",
            schema: "identity",
            table: "oidc_authorizations",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_authorizations_subject",
            schema: "identity",
            table: "oidc_authorizations",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ux_oidc_scopes_name",
            schema: "identity",
            table: "oidc_scopes",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_oidc_tokens_application_id",
            schema: "identity",
            table: "oidc_tokens",
            column: "application_id");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_tokens_authorization_id",
            schema: "identity",
            table: "oidc_tokens",
            column: "authorization_id");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_tokens_expires_at",
            schema: "identity",
            table: "oidc_tokens",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_oidc_tokens_subject",
            schema: "identity",
            table: "oidc_tokens",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ux_oidc_tokens_reference_id",
            schema: "identity",
            table: "oidc_tokens",
            column: "reference_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "oidc_scopes",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "oidc_tokens",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "oidc_authorizations",
            schema: "identity");

        migrationBuilder.CreateTable(
            name: "oidc_codes",
            schema: "identity",
            columns: table => new
            {
                fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                challenge = table.Column<string>(type: "text", nullable: false),
                challenge_method = table.Column<string>(type: "text", nullable: false),
                client_id = table.Column<string>(type: "text", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                nonce = table.Column<string>(type: "text", nullable: true),
                redirect = table.Column<string>(type: "text", nullable: false),
                scope = table.Column<string>(type: "text", nullable: false),
                session = table.Column<Guid>(type: "uuid", nullable: false),
                spent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                subject = table.Column<Guid>(type: "uuid", nullable: false)
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
                client_id = table.Column<string>(type: "text", nullable: false),
                consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                family = table.Column<Guid>(type: "uuid", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                scope = table.Column<string>(type: "text", nullable: false),
                session = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false)
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
    }
}
