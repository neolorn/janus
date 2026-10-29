using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddCollectionSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<Guid>(
            name: "collection",
            schema: "identity",
            table: "preauthentication_sessions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "collection_sessions",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                wrapped_key = table.Column<byte[]>(type: "bytea", nullable: false),
                enc_session = table.Column<byte[]>(type: "bytea", nullable: false),
            },
            constraints: table => table.PrimaryKey("pk_collection_sessions", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ux_preauthentication_sessions_collection",
            schema: "identity",
            table: "preauthentication_sessions",
            column: "collection",
            unique: true,
            filter: "collection IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_collection_sessions_expires_at",
            schema: "identity",
            table: "collection_sessions",
            column: "expires_at");

        migrationBuilder.AddForeignKey(
            name: "fk_preauthentication_sessions_collection",
            schema: "identity",
            table: "preauthentication_sessions",
            column: "collection",
            principalSchema: "identity",
            principalTable: "collection_sessions",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.collection_sessions TO identity_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropForeignKey(
            name: "fk_preauthentication_sessions_collection",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropTable(
            name: "collection_sessions",
            schema: "identity");

        migrationBuilder.DropIndex(
            name: "ux_preauthentication_sessions_collection",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropColumn(
            name: "collection",
            schema: "identity",
            table: "preauthentication_sessions");
    }
}
