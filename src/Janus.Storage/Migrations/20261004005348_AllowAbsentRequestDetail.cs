using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AllowAbsentRequestDetail : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AlterColumn<string>(
            name: "detail",
            schema: "identity",
            table: "privacy_requests",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        // 09 section 8a, D-183: a request entered with no detail was stored with an
        // empty text, and none is never an empty text.
        migrationBuilder.Sql("UPDATE identity.privacy_requests SET detail = NULL WHERE detail = '';");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql("UPDATE identity.privacy_requests SET detail = '' WHERE detail IS NULL;");

        migrationBuilder.AlterColumn<string>(
            name: "detail",
            schema: "identity",
            table: "privacy_requests",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }
}
