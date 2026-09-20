using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddSignIn : Migration
{
    private static readonly string[] OrganizationAndField = ["organization", "field"];
    private static readonly string[] SubjectAndFactor = ["subject", "factor"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "policy_raises",
            schema: "janus",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization = table.Column<Guid>(type: "uuid", nullable: true),
                field = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: false),
                raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_policy_raises", x => x.id);
                table.CheckConstraint("ck_policy_raises_field", "field IN ('credentialRedundancy', 'requiredAssurance')");
                table.ForeignKey(
                    name: "fk_policy_raises_organization",
                    column: x => x.organization,
                    principalSchema: "janus",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "signin_challenges",
            schema: "janus",
            columns: table => new
            {
                handle = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: true),
                webauthn = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                presented = table.Column<string[]>(type: "text[]", nullable: false),
                device_code = table.Column<byte[]>(type: "bytea", nullable: true),
                device_attempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_signin_challenges", x => x.handle);
                table.CheckConstraint("ck_signin_challenges_device_attempts", "device_attempts >= 0 AND (device_code IS NOT NULL OR device_attempts = 0)");
                table.CheckConstraint("ck_signin_challenges_handle", "octet_length(handle) = 32");
                table.ForeignKey(
                    name: "fk_signin_challenges_subject",
                    column: x => x.subject,
                    principalSchema: "janus",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "signin_links",
            schema: "janus",
            columns: table => new
            {
                token = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                factor = table.Column<string>(type: "text", nullable: false),
                enc_code = table.Column<byte[]>(type: "bytea", nullable: false),
                browser = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                wrong_attempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_signin_links", x => x.token);
                table.CheckConstraint("ck_signin_links_browser", "browser IS NULL OR octet_length(browser) = 32");
                table.CheckConstraint("ck_signin_links_factor", "factor IN ('apple', 'breakGlass', 'emailCode', 'emailLink', 'google', 'passkey', 'password', 'phoneCode', 'phoneLink', 'recoveryCodes', 'securityKey', 'totp')");
                table.CheckConstraint("ck_signin_links_token", "octet_length(token) = 32");
                table.CheckConstraint("ck_signin_links_wrong_attempts", "wrong_attempts >= 0");
                table.ForeignKey(
                    name: "fk_signin_links_subject",
                    column: x => x.subject,
                    principalSchema: "janus",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_policy_raises_field",
            schema: "janus",
            table: "policy_raises",
            column: "field",
            unique: true,
            filter: "organization IS NULL");

        migrationBuilder.CreateIndex(
            name: "ux_policy_raises_organization_field",
            schema: "janus",
            table: "policy_raises",
            columns: OrganizationAndField,
            unique: true,
            filter: "organization IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_signin_challenges_expires_at",
            schema: "janus",
            table: "signin_challenges",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_signin_challenges_subject",
            schema: "janus",
            table: "signin_challenges",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ix_signin_links_expires_at",
            schema: "janus",
            table: "signin_links",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ux_signin_links_subject_factor",
            schema: "janus",
            table: "signin_links",
            columns: SubjectAndFactor,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "policy_raises",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "signin_challenges",
            schema: "janus");

        migrationBuilder.DropTable(
            name: "signin_links",
            schema: "janus");
    }
}
