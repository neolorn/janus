using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class CollateGroupNamesAndCredentialLabels : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // OPS-DB-001 and INF-DB-001 AC3: written out because the provider quotes a
        // column's collation as one identifier, and this one is named by its schema. The
        // statement rebuilds ux_authenticators_label under the collation, so the index
        // refuses a label held in other capitals.
        migrationBuilder.Sql(
            "ALTER TABLE identity.groups "
            + "ALTER COLUMN name TYPE text COLLATE identity.identity_ci;");

        migrationBuilder.Sql(
            "ALTER TABLE identity.authenticators "
            + "ALTER COLUMN label TYPE character varying(64) COLLATE identity.identity_ci;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            "ALTER TABLE identity.authenticators "
            + "ALTER COLUMN label TYPE character varying(64) COLLATE \"default\";");

        migrationBuilder.Sql(
            "ALTER TABLE identity.groups "
            + "ALTER COLUMN name TYPE text COLLATE \"default\";");
    }
}
