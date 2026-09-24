using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddBulkExports : Migration
{
    private static readonly string[] ActorAndTime = ["actor", "principal", "admitted_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bulk_exports",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor = table.Column<Guid>(type: "uuid", nullable: true),
                principal = table.Column<string>(type: "text", nullable: true),
                admitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bulk_exports", x => x.id);
                table.CheckConstraint("ck_bulk_exports_actor", "(actor IS NULL) <> (principal IS NULL)");
            });

        migrationBuilder.CreateIndex(
            name: "ix_bulk_exports_actor",
            schema: "identity",
            table: "bulk_exports",
            columns: ActorAndTime);

        // OPS-MIG-003: the application counts an actor's exports, records each one and
        // forgets the actor's exports older than the hour the limit counts.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.bulk_exports TO identity_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "bulk_exports",
            schema: "identity");
    }
}
