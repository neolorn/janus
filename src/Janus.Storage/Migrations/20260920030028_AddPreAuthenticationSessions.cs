using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddPreAuthenticationSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "preauthentication_sessions",
            schema: "janus",
            columns: table => new
            {
                fingerprint = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                csrf_fingerprint = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                registration = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_preauthentication_sessions", x => x.fingerprint);
                table.CheckConstraint("ck_preauthentication_sessions_expires_at", "expires_at > created_at");
                table.CheckConstraint("ck_preauthentication_sessions_fingerprint", "octet_length(fingerprint) = 32 AND octet_length(csrf_fingerprint) = 32");
                table.ForeignKey(
                    name: "fk_preauthentication_sessions_registration",
                    column: x => x.registration,
                    principalSchema: "janus",
                    principalTable: "registration_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "ix_preauthentication_sessions_expires_at",
            schema: "janus",
            table: "preauthentication_sessions",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ux_preauthentication_sessions_registration",
            schema: "janus",
            table: "preauthentication_sessions",
            column: "registration",
            unique: true,
            filter: "registration IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "preauthentication_sessions",
            schema: "janus");
    }
}
