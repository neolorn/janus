using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddVerificationCodes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_signin_challenges_device_attempts",
            schema: "janus",
            table: "signin_challenges");

        migrationBuilder.DropColumn(
            name: "device_attempts",
            schema: "janus",
            table: "signin_challenges");

        migrationBuilder.DropColumn(
            name: "device_code",
            schema: "janus",
            table: "signin_challenges");

        migrationBuilder.CreateTable(
            name: "verification_codes",
            schema: "janus",
            columns: table => new
            {
                holder = table.Column<byte[]>(type: "bytea", nullable: false),
                code = table.Column<byte[]>(type: "bytea", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_verification_codes", x => x.holder);
                table.CheckConstraint("ck_verification_codes_attempts", "attempts >= 0");
                table.CheckConstraint("ck_verification_codes_holder", "octet_length(holder) > 0");
            });

        migrationBuilder.CreateIndex(
            name: "ix_verification_codes_expires_at",
            schema: "janus",
            table: "verification_codes",
            column: "expires_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "verification_codes",
            schema: "janus");

        migrationBuilder.AddColumn<int>(
            name: "device_attempts",
            schema: "janus",
            table: "signin_challenges",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<byte[]>(
            name: "device_code",
            schema: "janus",
            table: "signin_challenges",
            type: "bytea",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_signin_challenges_device_attempts",
            schema: "janus",
            table: "signin_challenges",
            sql: "device_attempts >= 0 AND (device_code IS NOT NULL OR device_attempts = 0)");
    }
}
