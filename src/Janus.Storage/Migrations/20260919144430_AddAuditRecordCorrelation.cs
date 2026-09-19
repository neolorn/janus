using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddAuditRecordCorrelation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTHZ-CONCEAL-004: a correlation identifier a support role is given resolves
        // to the one row that carries it. The table's key leads with the partition and
        // the instant, neither of which the identifier says, so the identifier carries
        // its own index.
        migrationBuilder.Sql(
            """
            CREATE INDEX ix_audit_records_id ON janus.audit_records (id);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql("DROP INDEX janus.ix_audit_records_id;");
    }
}
