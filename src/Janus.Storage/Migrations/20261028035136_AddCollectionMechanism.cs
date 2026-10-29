using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddCollectionMechanism : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        Admit(migrationBuilder, "consents", "'administrator', 'collection', 'dashboard', 'reconsent', 'registration'");
        Admit(migrationBuilder, "objections", "'administrator', 'collection', 'dashboard', 'reconsent', 'registration'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        Admit(migrationBuilder, "consents", "'administrator', 'dashboard', 'reconsent', 'registration'");
        Admit(migrationBuilder, "objections", "'administrator', 'dashboard', 'reconsent', 'registration'");
    }

    private static void Admit(MigrationBuilder migrationBuilder, string table, string mechanisms)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_" + table + "_mechanism",
            schema: "identity",
            table: table);

        migrationBuilder.AddCheckConstraint(
            name: "ck_" + table + "_mechanism",
            schema: "identity",
            table: table,
            sql: "mechanism IN (" + mechanisms + ")");
    }
}
