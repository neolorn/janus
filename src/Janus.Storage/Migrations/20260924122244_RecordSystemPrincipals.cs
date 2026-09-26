using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class RecordSystemPrincipals : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // IDN-PRIN-001 AC4, INF-BG-002 AC2: an action of background work names the
        // principal that took it and the reason it stated, never one without the other
        // and never beside an acting identity it does not have. The table is written by
        // hand rather than by the model builder, so this is too.
        migrationBuilder.Sql(
            """
            ALTER TABLE identity.audit_records
                ADD COLUMN principal text,
                ADD COLUMN principal_reason text,
                ADD CONSTRAINT ck_audit_records_principal CHECK (
                    (principal IS NULL AND principal_reason IS NULL)
                    OR (principal IS NOT NULL
                        AND principal_reason IS NOT NULL
                        AND acting_subject = '00000000-0000-0000-0000-000000000000'));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // Nothing removes an audit row (PRIV-RET-002 AC1), so the columns go with what
        // they hold only when no background action has been recorded in them.
        migrationBuilder.Sql(
            """
            DO $do$
            BEGIN
                IF EXISTS (SELECT FROM identity.audit_records WHERE principal IS NOT NULL) THEN
                    RAISE EXCEPTION 'The trail holds actions of background work.';
                END IF;
            END;
            $do$;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE identity.audit_records
                DROP CONSTRAINT ck_audit_records_principal,
                DROP COLUMN principal_reason,
                DROP COLUMN principal;
            """);
    }
}
