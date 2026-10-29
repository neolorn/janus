using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddRecordOffers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "record_offers",
            schema: "identity",
            columns: table => new
            {
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                proven = table.Column<string>(type: "jsonb", nullable: false),
                offered = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_record_offers", x => x.session_id);
                table.ForeignKey(
                    name: "fk_record_offers_session_id",
                    column: x => x.session_id,
                    principalSchema: "identity",
                    principalTable: "sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_record_offers_expires_at",
            schema: "identity",
            table: "record_offers",
            column: "expires_at");

        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.record_offers TO identity_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "record_offers",
            schema: "identity");
    }
}
