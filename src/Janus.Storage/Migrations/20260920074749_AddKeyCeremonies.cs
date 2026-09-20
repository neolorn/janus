using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddKeyCeremonies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "key_ceremonies",
            schema: "janus",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                challenge = table.Column<string>(type: "text", nullable: false),
                upgrading = table.Column<Guid>(type: "uuid", nullable: true),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_key_ceremonies", x => x.subject);
                table.CheckConstraint("ck_key_ceremonies_expiry", "expires_at > issued_at");
                table.CheckConstraint("ck_key_ceremonies_kind", "kind IN ('apple', 'breakGlass', 'emailCode', 'emailLink', 'google', 'passkey', 'password', 'phoneCode', 'phoneLink', 'recoveryCodes', 'securityKey', 'totp')");
                table.ForeignKey(
                    name: "fk_key_ceremonies_subject",
                    column: x => x.subject,
                    principalSchema: "janus",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_key_ceremonies_upgrading",
                    column: x => x.upgrading,
                    principalSchema: "janus",
                    principalTable: "authenticators",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_key_ceremonies_expires_at",
            schema: "janus",
            table: "key_ceremonies",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_key_ceremonies_upgrading",
            schema: "janus",
            table: "key_ceremonies",
            column: "upgrading");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "key_ceremonies",
            schema: "janus");
    }
}
