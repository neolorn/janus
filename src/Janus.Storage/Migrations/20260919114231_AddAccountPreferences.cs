using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddAccountPreferences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "account_preferences",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                language = table.Column<string>(type: "text", nullable: true),
                time_zone = table.Column<string>(type: "text", nullable: true),
                enc_values = table.Column<byte[]>(type: "bytea", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_account_preferences", x => x.subject);
                table.ForeignKey(
                    name: "fk_account_preferences_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "account_preferences",
            schema: "identity");
    }
}
