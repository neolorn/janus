using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class SpellTheMeasuresOrganizational : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // PRIV-ROPA-001 (D-187): every name the library gives the field spells it as
        // chapter 09 does. A rename: the statement the column holds stays.
        migrationBuilder.RenameColumn(
            name: "organisational_measures",
            schema: "identity",
            table: "compliance_records",
            newName: "organizational_measures");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.RenameColumn(
            name: "organizational_measures",
            schema: "identity",
            table: "compliance_records",
            newName: "organisational_measures");
    }
}
