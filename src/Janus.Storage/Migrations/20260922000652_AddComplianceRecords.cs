using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddComplianceRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "compliance_records",
            schema: "janus",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false),
                data_owner = table.Column<string>(type: "text", nullable: true),
                organisational_measures = table.Column<string>(type: "text", nullable: true),
                assessment_links = table.Column<string>(type: "jsonb", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_compliance_records", x => x.id);
                table.CheckConstraint("ck_compliance_records_only", "id = 1");
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "compliance_records",
            schema: "janus");
    }
}
