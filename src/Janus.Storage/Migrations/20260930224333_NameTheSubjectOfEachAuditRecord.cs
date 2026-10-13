using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class NameTheSubjectOfEachAuditRecord : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // IDN-AUD-001, AUTHZ-IMP-001, PRIV-BREACH-002 (D-166, 303): a record names the
        // data subject it concerns apart from both identities, and the trail by subject
        // reads it through an index on that column. No existing row is rewritten. The
        // subject is refused the max UUID, as every column holding a subject identifier
        // is (PRIV-RIGHT-005a).
        migrationBuilder.Sql(
            """
            ALTER TABLE identity.audit_records ADD COLUMN subject uuid;
            ALTER TABLE identity.audit_records ADD CONSTRAINT ck_audit_records_subject_not_max_uuid
                CHECK (subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'::uuid);
            CREATE INDEX ix_audit_records_subject ON identity.audit_records (subject, occurred_at);
            DROP INDEX identity.ix_audit_records_effective_subject;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            """
            CREATE INDEX ix_audit_records_effective_subject ON identity.audit_records (effective_subject, occurred_at);
            DROP INDEX identity.ix_audit_records_subject;
            ALTER TABLE identity.audit_records DROP CONSTRAINT ck_audit_records_subject_not_max_uuid;
            ALTER TABLE identity.audit_records DROP COLUMN subject;
            """);
    }
}
