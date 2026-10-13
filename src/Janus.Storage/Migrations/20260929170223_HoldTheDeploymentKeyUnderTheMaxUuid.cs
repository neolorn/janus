using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class HoldTheDeploymentKeyUnderTheMaxUuid : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // PRIV-RIGHT-005a, D-172, D-173: a value the field cipher encrypts for a row that
        // belongs to no subject is bound to its own row, where it was bound to the nil
        // subject (or, for a registration session, to its provisional subject). Binding
        // it again means decrypting it, which needs the key-encryption key the database
        // never holds, so the migration runs only where none stands, and names the table
        // where one does.
        migrationBuilder.Sql(
            """
            DO $do$
            BEGIN
                IF EXISTS (SELECT FROM identity.invitations WHERE enc_identifiers IS NOT NULL) THEN
                    RAISE EXCEPTION 'identity.invitations holds values not bound to their own rows.';
                END IF;

                IF EXISTS (SELECT FROM identity.mailboxes WHERE holder IS NULL) THEN
                    RAISE EXCEPTION 'identity.mailboxes holds values not bound to their own rows.';
                END IF;

                IF EXISTS (SELECT FROM identity.send_outbox WHERE subject IS NULL) THEN
                    RAISE EXCEPTION 'identity.send_outbox holds values not bound to their own rows.';
                END IF;

                IF EXISTS (SELECT FROM identity.registration_sessions) THEN
                    RAISE EXCEPTION 'identity.registration_sessions holds values not bound to their own rows.';
                END IF;
            END;
            $do$;
            """);

        // The deployment's data key leaves the nil subject, which means no subject, for
        // the max UUID of RFC 9562, which no subject is issued. Its wrap carries no
        // binding to the identifier, so the row moves as it is.
        migrationBuilder.Sql(
            """
            UPDATE identity.subject_keys
            SET subject = 'ffffffff-ffff-ffff-ffff-ffffffffffff'
            WHERE subject = '00000000-0000-0000-0000-000000000000';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // A value bound to its own row does not decrypt under the binding the previous
        // version reads, so the key goes back only where none stands.
        migrationBuilder.Sql(
            """
            DO $do$
            BEGIN
                IF EXISTS (SELECT FROM identity.invitations WHERE enc_identifiers IS NOT NULL) THEN
                    RAISE EXCEPTION 'identity.invitations holds values bound to their own rows.';
                END IF;

                IF EXISTS (SELECT FROM identity.mailboxes WHERE holder IS NULL) THEN
                    RAISE EXCEPTION 'identity.mailboxes holds values bound to their own rows.';
                END IF;

                IF EXISTS (SELECT FROM identity.send_outbox WHERE subject IS NULL) THEN
                    RAISE EXCEPTION 'identity.send_outbox holds values bound to their own rows.';
                END IF;

                IF EXISTS (SELECT FROM identity.registration_sessions) THEN
                    RAISE EXCEPTION 'identity.registration_sessions holds values bound to their own rows.';
                END IF;
            END;
            $do$;
            """);

        migrationBuilder.Sql(
            """
            UPDATE identity.subject_keys
            SET subject = '00000000-0000-0000-0000-000000000000'
            WHERE subject = 'ffffffff-ffff-ffff-ffff-ffffffffffff';
            """);
    }
}
