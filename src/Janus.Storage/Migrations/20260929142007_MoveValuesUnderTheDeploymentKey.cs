using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class MoveValuesUnderTheDeploymentKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // PRIV-RIGHT-005a, D-166 (316): a value that belongs to no subject is wrapped
        // under the deployment's data key, which the key-encryption key wraps as a row of
        // the subject-key table. A value wrapped under the key-encryption key itself
        // cannot be carried across by the database, which never holds that key, so the
        // migration runs only where none is held.
        migrationBuilder.Sql(
            """
            DO $do$
            BEGIN
                IF EXISTS (SELECT FROM identity.invitations WHERE wrapped_key IS NOT NULL)
                    OR EXISTS (SELECT FROM identity.mailboxes WHERE wrapped_key IS NOT NULL)
                    OR EXISTS (SELECT FROM identity.registration_sessions)
                    OR EXISTS (SELECT FROM identity.send_outbox)
                    OR EXISTS (SELECT FROM identity.signing_keys)
                    OR EXISTS (SELECT FROM identity.preauthentication_sessions WHERE signon_verifier IS NOT NULL)
                    OR EXISTS (SELECT FROM identity.provider_attempts WHERE verifier IS NOT NULL) THEN
                    RAISE EXCEPTION 'Values are held wrapped under the key-encryption key outside the subject-key table.';
                END IF;
            END;
            $do$;
            """);

        // OPS-SEC-003, OPS-MIG-003a AC4: the rotation re-wraps rows of the subject-key
        // table and nothing else, so the maintenance credential reaches none of these
        // columns. The fingerprint key's rotation still reads a reserved mailbox's own
        // key, to recompute its fingerprint from its address.
        migrationBuilder.Sql(
            """
            REVOKE SELECT (id, wrapped_key), UPDATE (wrapped_key)
                ON identity.invitations FROM identity_maintenance;
            REVOKE UPDATE (wrapped_key) ON identity.mailboxes FROM identity_maintenance;
            REVOKE SELECT (id, wrapped_key), UPDATE (wrapped_key)
                ON identity.registration_sessions FROM identity_maintenance;
            REVOKE SELECT (id, wrapped_key), UPDATE (wrapped_key)
                ON identity.send_outbox FROM identity_maintenance;
            REVOKE SELECT (key_id, private_key), UPDATE (private_key)
                ON identity.signing_keys FROM identity_maintenance;
            REVOKE SELECT (fingerprint, signon_verifier), UPDATE (signon_verifier)
                ON identity.preauthentication_sessions FROM identity_maintenance;
            REVOKE SELECT (id, verifier), UPDATE (verifier)
                ON identity.provider_attempts FROM identity_maintenance;
            """);

        migrationBuilder.DropIndex(
            name: "ix_signing_keys_key_version",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropCheckConstraint(
            name: "ck_signing_keys_version",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropCheckConstraint(
            name: "ck_provider_attempts_verifier",
            schema: "identity",
            table: "provider_attempts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_preauthentication_sessions_signon",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropCheckConstraint(
            name: "ck_mailboxes_key",
            schema: "identity",
            table: "mailboxes");

        migrationBuilder.DropCheckConstraint(
            name: "ck_invitations_key",
            schema: "identity",
            table: "invitations");

        migrationBuilder.DropColumn(
            name: "key_version",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropColumn(
            name: "key_version",
            schema: "identity",
            table: "send_outbox");

        migrationBuilder.DropColumn(
            name: "key_version",
            schema: "identity",
            table: "registration_sessions");

        migrationBuilder.DropColumn(
            name: "key_version",
            schema: "identity",
            table: "provider_attempts");

        migrationBuilder.DropColumn(
            name: "signon_key_version",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropColumn(
            name: "key_version",
            schema: "identity",
            table: "mailboxes");

        migrationBuilder.DropColumn(
            name: "key_version",
            schema: "identity",
            table: "invitations");

        migrationBuilder.AddCheckConstraint(
            name: "ck_preauthentication_sessions_signon",
            schema: "identity",
            table: "preauthentication_sessions",
            sql: "num_nulls(signon_state, signon_verifier, signon_return) IN (0, 3)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_mailboxes_key",
            schema: "identity",
            table: "mailboxes",
            sql: "(holder IS NULL) = (wrapped_key IS NOT NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_invitations_key",
            schema: "identity",
            table: "invitations",
            sql: "(enc_identifiers IS NULL) = (wrapped_key IS NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // A value wrapped under the deployment's data key names no version of the
        // key-encryption key, so the columns come back only where none is held.
        migrationBuilder.Sql(
            """
            DO $do$
            BEGIN
                IF EXISTS (SELECT FROM identity.invitations WHERE wrapped_key IS NOT NULL)
                    OR EXISTS (SELECT FROM identity.mailboxes WHERE wrapped_key IS NOT NULL)
                    OR EXISTS (SELECT FROM identity.registration_sessions)
                    OR EXISTS (SELECT FROM identity.send_outbox)
                    OR EXISTS (SELECT FROM identity.signing_keys)
                    OR EXISTS (SELECT FROM identity.preauthentication_sessions WHERE signon_verifier IS NOT NULL)
                    OR EXISTS (SELECT FROM identity.provider_attempts WHERE verifier IS NOT NULL) THEN
                    RAISE EXCEPTION 'Values are held wrapped under the deployment data key.';
                END IF;
            END;
            $do$;
            """);

        migrationBuilder.DropCheckConstraint(
            name: "ck_preauthentication_sessions_signon",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropCheckConstraint(
            name: "ck_mailboxes_key",
            schema: "identity",
            table: "mailboxes");

        migrationBuilder.DropCheckConstraint(
            name: "ck_invitations_key",
            schema: "identity",
            table: "invitations");

        migrationBuilder.AddColumn<int>(
            name: "key_version",
            schema: "identity",
            table: "signing_keys",
            type: "integer",
            nullable: false);

        migrationBuilder.AddColumn<int>(
            name: "key_version",
            schema: "identity",
            table: "send_outbox",
            type: "integer",
            nullable: false);

        migrationBuilder.AddColumn<int>(
            name: "key_version",
            schema: "identity",
            table: "registration_sessions",
            type: "integer",
            nullable: false);

        migrationBuilder.AddColumn<int>(
            name: "key_version",
            schema: "identity",
            table: "provider_attempts",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "signon_key_version",
            schema: "identity",
            table: "preauthentication_sessions",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "key_version",
            schema: "identity",
            table: "mailboxes",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "key_version",
            schema: "identity",
            table: "invitations",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_signing_keys_key_version",
            schema: "identity",
            table: "signing_keys",
            column: "key_version");

        migrationBuilder.AddCheckConstraint(
            name: "ck_signing_keys_version",
            schema: "identity",
            table: "signing_keys",
            sql: "key_version >= 1");

        migrationBuilder.AddCheckConstraint(
            name: "ck_provider_attempts_verifier",
            schema: "identity",
            table: "provider_attempts",
            sql: "num_nulls(verifier, key_version) IN (0, 2)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_preauthentication_sessions_signon",
            schema: "identity",
            table: "preauthentication_sessions",
            sql: "num_nulls(signon_state, signon_verifier, signon_key_version, signon_return) IN (0, 4)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_mailboxes_key",
            schema: "identity",
            table: "mailboxes",
            sql: "(holder IS NULL) = (wrapped_key IS NOT NULL) AND (wrapped_key IS NULL) = (key_version IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_invitations_key",
            schema: "identity",
            table: "invitations",
            sql: "(enc_identifiers IS NULL) = (wrapped_key IS NULL) AND (wrapped_key IS NULL) = (key_version IS NULL)");

        migrationBuilder.Sql(
            """
            GRANT SELECT (id, key_version, verifier), UPDATE (key_version, verifier)
                ON identity.provider_attempts TO identity_maintenance;
            GRANT SELECT (fingerprint, signon_key_version, signon_verifier),
                  UPDATE (signon_key_version, signon_verifier)
                ON identity.preauthentication_sessions TO identity_maintenance;
            GRANT SELECT (key_id, key_version, private_key), UPDATE (key_version, private_key)
                ON identity.signing_keys TO identity_maintenance;
            GRANT SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.send_outbox TO identity_maintenance;
            GRANT SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.registration_sessions TO identity_maintenance;
            GRANT SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.mailboxes TO identity_maintenance;
            GRANT SELECT (id, key_version, wrapped_key), UPDATE (key_version, wrapped_key)
                ON identity.invitations TO identity_maintenance;
            """);
    }
}
