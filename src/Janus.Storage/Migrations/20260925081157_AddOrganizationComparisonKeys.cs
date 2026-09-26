using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddOrganizationComparisonKeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // IDN-ACCT-004 and OPS-MIG-005 AC1: the key is added nullable, and the
        // constraint is tightened in a later release.
        migrationBuilder.AddColumn<string>(
            name: "canonical_name",
            schema: "identity",
            table: "organizations",
            type: "text",
            nullable: true);

        // OPS-DB-001 and INF-DB-001 AC3: written out because the provider quotes a
        // column's collation as one identifier, and this one is named by its schema.
        migrationBuilder.Sql(
            "ALTER TABLE identity.organization_domains "
            + "ALTER COLUMN domain TYPE text COLLATE identity.identity_ci;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "canonical_name",
            schema: "identity",
            table: "organizations");

        migrationBuilder.Sql(
            "ALTER TABLE identity.organization_domains "
            + "ALTER COLUMN domain TYPE text COLLATE \"default\";");
    }
}
