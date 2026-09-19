using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddProfiles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "profiles",
            schema: "janus",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                enc_display_name = table.Column<byte[]>(type: "bytea", nullable: true),
                enc_legal_name = table.Column<byte[]>(type: "bytea", nullable: true),
                enc_date_of_birth = table.Column<byte[]>(type: "bytea", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_profiles", x => x.subject);
                table.ForeignKey(
                    name: "fk_profiles_subject",
                    column: x => x.subject,
                    principalSchema: "janus",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "profiles",
            schema: "janus");
    }
}
