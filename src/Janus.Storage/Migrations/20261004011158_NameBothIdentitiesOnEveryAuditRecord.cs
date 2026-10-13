using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class NameBothIdentitiesOnEveryAuditRecord : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTHZ-CONCEAL-004, IDN-AUD-001 AC1 (D-166, 136): a refusal of background work
        // names the nil subject under both identities beside its principal, so no record
        // names nobody. Nothing rewrites an audit row (PRIV-RET-002 AC1), so a trail
        // that holds a refusal naming nobody stops here rather than being changed. The
        // table is written by hand rather than by the model builder, so this is too.
        migrationBuilder.Sql(
            """
            DO $do$
            BEGIN
                IF EXISTS (
                    SELECT FROM identity.audit_records
                    WHERE acting_subject IS NULL OR effective_subject IS NULL) THEN
                    RAISE EXCEPTION 'The trail holds records naming no acting or no effective identity.';
                END IF;
            END;
            $do$;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE identity.audit_records DROP CONSTRAINT ck_audit_records_identities;
            ALTER TABLE identity.audit_records
                ALTER COLUMN acting_subject SET NOT NULL,
                ALTER COLUMN effective_subject SET NOT NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            """
            ALTER TABLE identity.audit_records
                ALTER COLUMN acting_subject DROP NOT NULL,
                ALTER COLUMN effective_subject DROP NOT NULL;
            ALTER TABLE identity.audit_records
                ADD CONSTRAINT ck_audit_records_identities CHECK (
                    action = 'authz.access.denied'
                    OR (acting_subject IS NOT NULL AND effective_subject IS NOT NULL));
            """);
    }
}
