using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddKeyRotations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "key_rotations",
            schema: "identity",
            columns: table => new
            {
                kind = table.Column<string>(type: "text", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                last_subject = table.Column<Guid>(type: "uuid", nullable: true),
                processed = table.Column<int>(type: "integer", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_key_rotations", x => new { x.kind, x.version });
                table.CheckConstraint("ck_key_rotations_kind", "kind IN ('fingerprint-key', 'key-encryption-key')");
                table.CheckConstraint("ck_key_rotations_processed", "processed >= 0");
                table.CheckConstraint("ck_key_rotations_retired", "retired_at IS NULL OR completed_at IS NOT NULL");
                table.CheckConstraint("ck_key_rotations_version", "version >= 1");
            });

        // OPS-MIG-003a AC4: the rotation's progress is the maintenance credential's to
        // read and write, and the application's credential reaches none of it.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE ON identity.key_rotations TO identity_maintenance;
            """);

        // OPS-SEC-003 AC5: the rotation records its start, resumption, completion and
        // retirement, and appends to the trail as the application does, no more.
        migrationBuilder.Sql("GRANT INSERT ON identity.audit_records TO identity_maintenance;");

        // OPS-SEC-003 AC2, AC3: every value wrapped under the key is re-wrapped before
        // the previous version is retired, so the rotation reaches the key of each row,
        // the version and the wrapped value, and no other column (entry 316 of the
        // decisions pending review).
        migrationBuilder.Sql(
            """
            GRANT SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.invitations TO identity_maintenance;
            GRANT SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.mailboxes TO identity_maintenance;
            GRANT SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.registration_sessions TO identity_maintenance;
            GRANT SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.send_outbox TO identity_maintenance;
            GRANT SELECT (key_id, key_version, private_key), UPDATE (key_version, private_key)
                ON identity.signing_keys TO identity_maintenance;
            GRANT SELECT (fingerprint, signon_key_version, signon_verifier),
                  UPDATE (signon_key_version, signon_verifier)
                ON identity.preauthentication_sessions TO identity_maintenance;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            """
            REVOKE SELECT (fingerprint, signon_key_version, signon_verifier),
                   UPDATE (signon_key_version, signon_verifier)
                ON identity.preauthentication_sessions FROM identity_maintenance;
            REVOKE SELECT (key_id, key_version, private_key), UPDATE (key_version, private_key)
                ON identity.signing_keys FROM identity_maintenance;
            REVOKE SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.send_outbox FROM identity_maintenance;
            REVOKE SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.registration_sessions FROM identity_maintenance;
            REVOKE SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.mailboxes FROM identity_maintenance;
            REVOKE SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.invitations FROM identity_maintenance;
            """);

        migrationBuilder.Sql("REVOKE INSERT ON identity.audit_records FROM identity_maintenance;");

        migrationBuilder.DropTable(
            name: "key_rotations",
            schema: "identity");
    }
}
