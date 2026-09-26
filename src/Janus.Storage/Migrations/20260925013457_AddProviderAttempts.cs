using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddProviderAttempts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "provider_attempts",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                preauthentication = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                session = table.Column<Guid>(type: "uuid", nullable: true),
                provider = table.Column<string>(type: "text", nullable: false),
                intent = table.Column<string>(type: "text", nullable: false),
                state = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                nonce = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                verifier = table.Column<byte[]>(type: "bytea", nullable: true),
                key_version = table.Column<int>(type: "integer", nullable: true),
                return_to = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_provider_attempts", x => x.id);
                table.CheckConstraint("ck_provider_attempts_binding", "num_nonnulls(preauthentication, session) = 1");
                table.CheckConstraint("ck_provider_attempts_fingerprints", "octet_length(state) = 32 AND octet_length(nonce) = 32");
                table.CheckConstraint("ck_provider_attempts_intent", "intent IN ('link', 'register', 'signin')");
                table.CheckConstraint("ck_provider_attempts_provider", "provider IN ('apple', 'breakGlass', 'emailCode', 'emailLink', 'google', 'passkey', 'password', 'phoneCode', 'phoneLink', 'recoveryCodes', 'securityKey', 'totp')");
                table.CheckConstraint("ck_provider_attempts_verifier", "num_nulls(verifier, key_version) IN (0, 2)");
                table.ForeignKey(
                    name: "fk_provider_attempts_preauthentication",
                    column: x => x.preauthentication,
                    principalSchema: "identity",
                    principalTable: "preauthentication_sessions",
                    principalColumn: "fingerprint",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_provider_attempts_session",
                    column: x => x.session,
                    principalSchema: "identity",
                    principalTable: "sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ux_provider_attempts_preauthentication",
            schema: "identity",
            table: "provider_attempts",
            column: "preauthentication",
            unique: true,
            filter: "preauthentication IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_provider_attempts_session",
            schema: "identity",
            table: "provider_attempts",
            column: "session",
            unique: true,
            filter: "session IS NOT NULL");

        // OPS-MIG-003: the application starts, replaces and ends the round trips.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.provider_attempts TO identity_app;
            """);

        // OPS-SEC-003 AC2, AC3: the proof key is wrapped under the key-encryption key, so
        // the rotation reaches the row's key, the version and the wrapped value, and no
        // other column (entry 316 of the decisions pending review).
        migrationBuilder.Sql(
            """
            GRANT SELECT (id, key_version, verifier), UPDATE (key_version, verifier)
                ON identity.provider_attempts TO identity_maintenance;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "provider_attempts",
            schema: "identity");
    }
}
