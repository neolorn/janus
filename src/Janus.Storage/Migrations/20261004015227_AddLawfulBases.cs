using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddLawfulBases : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "lawful_bases",
            schema: "identity",
            columns: table => new
            {
                key = table.Column<string>(type: "text", nullable: false),
                label = table.Column<string>(type: "text", nullable: false),
                is_consent = table.Column<bool>(type: "boolean", nullable: false),
                requires_written_consent_for_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                requires_assessment = table.Column<bool>(type: "boolean", nullable: false),
                is_objectable = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_lawful_bases", x => x.key));

        // PRIV-BASIS-001 (D-183): the startup service writes the declared list under the
        // application's runtime credential, and no other credential writes the table.
        migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON identity.lawful_bases TO identity_app;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "lawful_bases",
            schema: "identity");
    }
}
