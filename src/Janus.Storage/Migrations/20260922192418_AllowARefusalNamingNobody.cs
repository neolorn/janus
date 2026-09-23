using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AllowARefusalNamingNobody : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // AUTHZ-CONCEAL-004: a request made under no account is refused with a
        // correlation identifier like any other, so the row it resolves to names
        // nobody. The table is written by hand rather than by the model builder, so
        // this is too.
        migrationBuilder.Sql(
            """
            ALTER TABLE janus.audit_records
                ALTER COLUMN acting_subject DROP NOT NULL,
                ALTER COLUMN effective_subject DROP NOT NULL;
            """);

        // IDN-AUD-001 AC1: every other event names both identities, which the database
        // refuses to be without rather than the code remembering to write them.
        migrationBuilder.Sql(
            """
            ALTER TABLE janus.audit_records
                ADD CONSTRAINT ck_audit_records_identities CHECK (
                    action = 'authz.access.denied'
                    OR (acting_subject IS NOT NULL AND effective_subject IS NOT NULL));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE janus.audit_records DROP CONSTRAINT ck_audit_records_identities;
            """);

        // Nothing removes an audit row (PRIV-RET-002 AC1), so a rollback over a trail
        // that already holds a refusal naming nobody fails here rather than deleting it.
        migrationBuilder.Sql(
            """
            ALTER TABLE janus.audit_records
                ALTER COLUMN acting_subject SET NOT NULL,
                ALTER COLUMN effective_subject SET NOT NULL;
            """);
    }
}
