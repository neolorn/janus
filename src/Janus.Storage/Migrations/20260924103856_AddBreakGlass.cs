using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddBreakGlass : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "emergency",
            schema: "identity",
            table: "accounts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "break_glass_attempts",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_break_glass_attempts", x => x.id));

        migrationBuilder.CreateTable(
            name: "break_glass_credentials",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                hash = table.Column<string>(type: "text", nullable: false),
                issued_by = table.Column<Guid>(type: "uuid", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                replaced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_break_glass_credentials", x => x.id);
                table.CheckConstraint("ck_break_glass_credentials_ended", "consumed_at IS NULL OR replaced_at IS NULL");
                table.ForeignKey(
                    name: "fk_break_glass_credentials_issued_by",
                    column: x => x.issued_by,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        // OPS-BOOT-004: at most one issue stands. The index is over a constant, which no
        // model builder states, so it is written here and read with the configuration.
        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX ux_break_glass_credentials_standing
                ON identity.break_glass_credentials ((true))
                WHERE consumed_at IS NULL AND replaced_at IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "ux_accounts_emergency",
            schema: "identity",
            table: "accounts",
            column: "emergency",
            unique: true,
            filter: "emergency");

        migrationBuilder.CreateIndex(
            name: "ix_break_glass_attempts_attempted_at",
            schema: "identity",
            table: "break_glass_attempts",
            column: "attempted_at");

        migrationBuilder.CreateIndex(
            name: "ix_break_glass_credentials_issued_by",
            schema: "identity",
            table: "break_glass_credentials",
            column: "issued_by");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX identity.ux_break_glass_credentials_standing;");

        migrationBuilder.DropTable(
            name: "break_glass_attempts",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "break_glass_credentials",
            schema: "identity");

        migrationBuilder.DropIndex(
            name: "ux_accounts_emergency",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "emergency",
            schema: "identity",
            table: "accounts");
    }
}
