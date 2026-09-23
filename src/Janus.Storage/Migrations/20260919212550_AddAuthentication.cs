using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddAuthentication : Migration
{
    private static readonly string[] SubjectAndEndedAt = ["subject", "ended_at"];
    private static readonly string[] SubjectFactorAndLabel = ["subject", "factor", "label"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "authenticators",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                factor = table.Column<string>(type: "text", nullable: false),
                label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                state = table.Column<string>(type: "text", nullable: false),
                added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                invalidates_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                confirmed = table.Column<bool>(type: "boolean", nullable: false),
                totp_secret = table.Column<byte[]>(type: "bytea", nullable: true),
                totp_consumed_step = table.Column<long>(type: "bigint", nullable: true),
                credential_id = table.Column<byte[]>(type: "bytea", nullable: true),
                public_key = table.Column<byte[]>(type: "bytea", nullable: true),
                algorithm = table.Column<int>(type: "integer", nullable: true),
                relying_party = table.Column<string>(type: "text", nullable: true),
                counter = table.Column<long>(type: "bigint", nullable: true),
                backup_eligible = table.Column<bool>(type: "boolean", nullable: true),
                backup_state = table.Column<bool>(type: "boolean", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_authenticators", x => x.id);
                table.CheckConstraint("ck_authenticators_factor", "factor IN ('apple', 'breakGlass', 'emailCode', 'emailLink', 'google', 'passkey', 'password', 'phoneCode', 'phoneLink', 'recoveryCodes', 'securityKey', 'totp')");
                table.CheckConstraint("ck_authenticators_invalidates_at", "invalidates_at IS NULL OR state IN ('suspended')");
                table.CheckConstraint("ck_authenticators_state", "state IN ('active', 'invalidated', 'suspended')");
                table.CheckConstraint("ck_authenticators_webauthn", "(credential_id IS NULL) = (public_key IS NULL) AND (credential_id IS NULL) = (algorithm IS NULL) AND (credential_id IS NULL) = (relying_party IS NULL) AND (credential_id IS NULL) = (backup_eligible IS NULL) AND (credential_id IS NULL) = (backup_state IS NULL)");
                table.ForeignKey(
                    name: "fk_authenticators_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "devices",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                token_fingerprint = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                revoked = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_devices", x => x.id);
                table.CheckConstraint("ck_devices_kind", "kind IN ('remembered', 'trusted')");
                table.ForeignKey(
                    name: "fk_devices_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "passwords",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                hash = table.Column<string>(type: "text", nullable: false),
                meets_single_factor_floor = table.Column<bool>(type: "boolean", nullable: false),
                set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_passwords", x => x.subject);
                table.ForeignKey(
                    name: "fk_passwords_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "recovery_code_sets",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                viewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                exported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                reminded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_recovery_code_sets", x => x.subject);
                table.ForeignKey(
                    name: "fk_recovery_code_sets_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "sessions",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                spine = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "text", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                secret_fingerprint = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                attained = table.Column<string>(type: "text", nullable: false),
                attained_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                phishing_resistant = table.Column<bool>(type: "boolean", nullable: false),
                phishing_resistant_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                origin_browser = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                origin_os = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                origin_place = table.Column<byte[]>(type: "bytea", nullable: false),
                last_seen_browser = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                last_seen_os = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                last_seen_place = table.Column<byte[]>(type: "bytea", nullable: false),
                idle_expiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                absolute_expiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                satisfies_every_gate = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_sessions", x => x.id);
                table.CheckConstraint("ck_sessions_attained", "attained IN ('aal1', 'aal2', 'aal3', 'delegated')");
                table.CheckConstraint("ck_sessions_phishing_resistant", "phishing_resistant = (phishing_resistant_at IS NOT NULL)");
                table.CheckConstraint("ck_sessions_type", "type IN ('auth', 'oidc-token', 'per-app')");
                table.ForeignKey(
                    name: "fk_sessions_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "recovery_codes",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                ordinal = table.Column<int>(type: "integer", nullable: false),
                hash = table.Column<string>(type: "text", nullable: false),
                used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_recovery_codes", x => new { x.subject, x.ordinal });
                table.ForeignKey(
                    name: "fk_recovery_codes_set",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "recovery_code_sets",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ux_authenticators_credential_id",
            schema: "identity",
            table: "authenticators",
            column: "credential_id",
            unique: true,
            filter: "credential_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_authenticators_label",
            schema: "identity",
            table: "authenticators",
            columns: SubjectFactorAndLabel,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_devices_subject",
            schema: "identity",
            table: "devices",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ux_devices_token_fingerprint",
            schema: "identity",
            table: "devices",
            column: "token_fingerprint",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_sessions_spine",
            schema: "identity",
            table: "sessions",
            column: "spine");

        migrationBuilder.CreateIndex(
            name: "ix_sessions_subject",
            schema: "identity",
            table: "sessions",
            columns: SubjectAndEndedAt);

        migrationBuilder.CreateIndex(
            name: "ux_sessions_secret_fingerprint",
            schema: "identity",
            table: "sessions",
            column: "secret_fingerprint",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "authenticators",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "devices",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "passwords",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "recovery_codes",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "sessions",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "recovery_code_sets",
            schema: "identity");
    }
}
