using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class WaitAnAuthorizationForItsSignIn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "pending_authorizations",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                preauthentication = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                session = table.Column<Guid>(type: "uuid", nullable: true),
                parameters = table.Column<byte[]>(type: "bytea", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pending_authorizations", x => x.id);
                table.CheckConstraint("ck_pending_authorizations_bound", "num_nonnulls(preauthentication, session) = 1");
                table.CheckConstraint("ck_pending_authorizations_expires_at", "expires_at > created_at");
                table.ForeignKey(
                    name: "fk_pending_authorizations_preauthentication",
                    column: x => x.preauthentication,
                    principalSchema: "identity",
                    principalTable: "preauthentication_sessions",
                    principalColumn: "fingerprint",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_pending_authorizations_session",
                    column: x => x.session,
                    principalSchema: "identity",
                    principalTable: "sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_pending_authorizations_expires_at",
            schema: "identity",
            table: "pending_authorizations",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ux_pending_authorizations_preauthentication",
            schema: "identity",
            table: "pending_authorizations",
            column: "preauthentication",
            unique: true,
            filter: "preauthentication IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_pending_authorizations_session",
            schema: "identity",
            table: "pending_authorizations",
            column: "session",
            unique: true,
            filter: "session IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "pending_authorizations",
            schema: "identity");
    }
}
