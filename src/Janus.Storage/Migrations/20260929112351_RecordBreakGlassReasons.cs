using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class RecordBreakGlassReasons : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // OPS-BOOT-002 AC10, D-170: the session the credential opens keeps the reason
        // given at its use, and no session another way in opens has one.
        migrationBuilder.AddColumn<string>(
            name: "breakglass_reason",
            schema: "identity",
            table: "sessions",
            type: "text",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_sessions_breakglass_reason",
            schema: "identity",
            table: "sessions",
            sql: "breakglass_reason IS NULL OR (satisfies_every_gate AND length(btrim(breakglass_reason)) BETWEEN 1 AND 1024)");

        // IDN-AUD-001, D-170: a record written in that session carries the reason in a
        // field of its own, and background work, which is no session, carries none.
        // The table is written by hand rather than by the model builder, so this is too.
        migrationBuilder.Sql(
            """
            ALTER TABLE identity.audit_records
                ADD COLUMN breakglass_reason text,
                ADD CONSTRAINT ck_audit_records_breakglass_reason CHECK (
                    breakglass_reason IS NULL
                    OR (principal IS NULL AND length(btrim(breakglass_reason)) BETWEEN 1 AND 1024));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // Nothing removes an audit row (PRIV-RET-002 AC1), so the column goes with what
        // it holds only when no break-glass session has written to the trail.
        migrationBuilder.Sql(
            """
            DO $do$
            BEGIN
                IF EXISTS (SELECT FROM identity.audit_records WHERE breakglass_reason IS NOT NULL) THEN
                    RAISE EXCEPTION 'The trail holds actions of a break-glass session.';
                END IF;
            END;
            $do$;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE identity.audit_records
                DROP CONSTRAINT ck_audit_records_breakglass_reason,
                DROP COLUMN breakglass_reason;
            """);

        migrationBuilder.DropCheckConstraint(
            name: "ck_sessions_breakglass_reason",
            schema: "identity",
            table: "sessions");

        migrationBuilder.DropColumn(
            name: "breakglass_reason",
            schema: "identity",
            table: "sessions");
    }
}
