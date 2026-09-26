using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddFingerprintVersions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_authenticators_provider_subject",
            schema: "identity",
            table: "authenticators");

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "username_holds",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "throttle_counters",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "sends",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "send_grants",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "send_counters",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "registration_sources",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "nonexistence_notices",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "mailboxes",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "identifiers",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "identifier_removals",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "callbacks",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<byte[]>(
            name: "enc_provider_subject",
            schema: "identity",
            table: "authenticators",
            type: "bytea",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "authenticators",
            type: "integer",
            nullable: true);

        // OPS-SEC-003: every fingerprint stored before the fingerprint key had versions
        // was computed under the one key there was, which a deployment's first key
        // document names version 1 (entry 318 of the decisions pending review). The
        // default serves those rows alone; every write names its version.
        migrationBuilder.Sql(
            """
            ALTER TABLE identity.username_holds ALTER COLUMN fingerprint_version DROP DEFAULT;
            ALTER TABLE identity.throttle_counters ALTER COLUMN fingerprint_version DROP DEFAULT;
            ALTER TABLE identity.sends ALTER COLUMN fingerprint_version DROP DEFAULT;
            ALTER TABLE identity.send_grants ALTER COLUMN fingerprint_version DROP DEFAULT;
            ALTER TABLE identity.send_counters ALTER COLUMN fingerprint_version DROP DEFAULT;
            ALTER TABLE identity.registration_sources ALTER COLUMN fingerprint_version DROP DEFAULT;
            ALTER TABLE identity.nonexistence_notices ALTER COLUMN fingerprint_version DROP DEFAULT;
            ALTER TABLE identity.mailboxes ALTER COLUMN fingerprint_version DROP DEFAULT;
            ALTER TABLE identity.identifiers ALTER COLUMN fingerprint_version DROP DEFAULT;
            ALTER TABLE identity.identifier_removals ALTER COLUMN fingerprint_version DROP DEFAULT;
            ALTER TABLE identity.callbacks ALTER COLUMN fingerprint_version DROP DEFAULT;
            """);

        migrationBuilder.AddCheckConstraint(
            name: "ck_authenticators_provider_subject",
            schema: "identity",
            table: "authenticators",
            sql: "(provider_subject IS NULL) <> (factor IN ('apple', 'google')) AND (provider_subject IS NULL OR octet_length(provider_subject) = 32) AND (provider_subject IS NULL) = (fingerprint_version IS NULL) AND (provider_subject IS NULL) = (enc_provider_subject IS NULL)");

        // OPS-SEC-003 AC6: the rotation computes each fingerprint again from the value
        // beside it, decrypted under the key it is held under, so it reaches the row's
        // key, its subject, the fingerprint, its version and the encrypted value, and
        // writes the fingerprint and its version and no other column. What no value
        // stands behind it reads for its version and forgets once retired (entry 318 of
        // the decisions pending review).
        migrationBuilder.Sql(
            """
            GRANT SELECT (identifier_id, subject, fingerprint, fingerprint_version, enc_canonical),
                  UPDATE (fingerprint, fingerprint_version)
                ON identity.identifiers TO identity_maintenance;
            GRANT SELECT (identifier_id, subject, fingerprint, fingerprint_version, enc_canonical, expires_at),
                  UPDATE (fingerprint, fingerprint_version)
                ON identity.identifier_removals TO identity_maintenance;
            GRANT SELECT (id, subject, provider_subject, fingerprint_version, enc_provider_subject),
                  UPDATE (provider_subject, fingerprint_version)
                ON identity.authenticators TO identity_maintenance;
            GRANT SELECT (holder, fingerprint, fingerprint_version, enc_canonical),
                  UPDATE (fingerprint, fingerprint_version)
                ON identity.mailboxes TO identity_maintenance;
            GRANT SELECT (fingerprint_version, releases_at), DELETE
                ON identity.username_holds TO identity_maintenance;
            GRANT SELECT (fingerprint_version), DELETE ON identity.callbacks TO identity_maintenance;
            GRANT SELECT (fingerprint_version), DELETE ON identity.nonexistence_notices TO identity_maintenance;
            GRANT SELECT (fingerprint_version), DELETE ON identity.registration_sources TO identity_maintenance;
            GRANT SELECT (fingerprint_version), DELETE ON identity.send_counters TO identity_maintenance;
            GRANT SELECT (fingerprint_version), DELETE ON identity.send_grants TO identity_maintenance;
            GRANT SELECT (fingerprint_version), DELETE ON identity.sends TO identity_maintenance;
            GRANT SELECT (fingerprint_version), DELETE ON identity.throttle_counters TO identity_maintenance;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            """
            REVOKE SELECT (fingerprint_version), DELETE ON identity.throttle_counters FROM identity_maintenance;
            REVOKE SELECT (fingerprint_version), DELETE ON identity.sends FROM identity_maintenance;
            REVOKE SELECT (fingerprint_version), DELETE ON identity.send_grants FROM identity_maintenance;
            REVOKE SELECT (fingerprint_version), DELETE ON identity.send_counters FROM identity_maintenance;
            REVOKE SELECT (fingerprint_version), DELETE ON identity.registration_sources FROM identity_maintenance;
            REVOKE SELECT (fingerprint_version), DELETE ON identity.nonexistence_notices FROM identity_maintenance;
            REVOKE SELECT (fingerprint_version), DELETE ON identity.callbacks FROM identity_maintenance;
            REVOKE SELECT (fingerprint_version, releases_at), DELETE
                ON identity.username_holds FROM identity_maintenance;
            REVOKE SELECT (holder, fingerprint, fingerprint_version, enc_canonical),
                   UPDATE (fingerprint, fingerprint_version)
                ON identity.mailboxes FROM identity_maintenance;
            REVOKE SELECT (id, subject, provider_subject, fingerprint_version, enc_provider_subject),
                   UPDATE (provider_subject, fingerprint_version)
                ON identity.authenticators FROM identity_maintenance;
            REVOKE SELECT (identifier_id, subject, fingerprint, fingerprint_version, enc_canonical, expires_at),
                   UPDATE (fingerprint, fingerprint_version)
                ON identity.identifier_removals FROM identity_maintenance;
            REVOKE SELECT (identifier_id, subject, fingerprint, fingerprint_version, enc_canonical),
                   UPDATE (fingerprint, fingerprint_version)
                ON identity.identifiers FROM identity_maintenance;
            """);

        migrationBuilder.DropCheckConstraint(
            name: "ck_authenticators_provider_subject",
            schema: "identity",
            table: "authenticators");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "username_holds");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "throttle_counters");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "sends");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "send_grants");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "send_counters");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "registration_sources");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "nonexistence_notices");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "mailboxes");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "identifiers");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "identifier_removals");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "callbacks");

        migrationBuilder.DropColumn(
            name: "enc_provider_subject",
            schema: "identity",
            table: "authenticators");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "authenticators");

        migrationBuilder.AddCheckConstraint(
            name: "ck_authenticators_provider_subject",
            schema: "identity",
            table: "authenticators",
            sql: "(provider_subject IS NULL) <> (factor IN ('apple', 'google')) AND (provider_subject IS NULL OR octet_length(provider_subject) = 32)");
    }
}
