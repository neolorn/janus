using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddConfigurationChangeIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // OPS-CFG-005 AC2: every change to one setting, without a scan of the trail.
        // The key is a field of the record's details, which no model builder expresses
        // as an index, and the containment the reader asks with is what a GIN index
        // over the document answers.
        migrationBuilder.Sql(
            """
            CREATE INDEX ix_audit_records_configuration_key
                ON janus.audit_records USING gin (details jsonb_path_ops)
                WHERE action = 'ops.configuration.changed';
            """);

        // OPS-CFG-005 AC2: every change by one actor. The trail is indexed by the
        // effective subject for PRIV-BREACH-002; this is the acting one, which is what
        // "by actor" asks for and what an impersonated change would differ on.
        migrationBuilder.Sql(
            """
            CREATE INDEX ix_audit_records_acting_subject
                ON janus.audit_records (acting_subject, occurred_at);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX janus.ix_audit_records_acting_subject;");
        migrationBuilder.Sql("DROP INDEX janus.ix_audit_records_configuration_key;");
    }
}
