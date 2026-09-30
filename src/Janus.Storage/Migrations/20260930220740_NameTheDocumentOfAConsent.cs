using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class NameTheDocumentOfAConsent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<string>(
            name: "document",
            schema: "identity",
            table: "consents",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "document",
            schema: "identity",
            table: "objections",
            type: "text",
            nullable: true);

        // PRIV-CONS-001, D-166: a record written before this named only a version, and
        // every purpose then was governed by the privacy notice.
        migrationBuilder.Sql("UPDATE identity.consents SET document = 'privacy-notice';");
        migrationBuilder.Sql("UPDATE identity.objections SET document = 'privacy-notice';");

        migrationBuilder.AlterColumn<string>(
            name: "document",
            schema: "identity",
            table: "consents",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "document",
            schema: "identity",
            table: "objections",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "document",
            schema: "identity",
            table: "objections");

        migrationBuilder.DropColumn(
            name: "document",
            schema: "identity",
            table: "consents");
    }
}
