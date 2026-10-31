using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class BindASignInToItsBrowser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<byte[]>(
            name: "browser",
            schema: "identity",
            table: "signin_challenges",
            type: "bytea",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "held_at",
            schema: "identity",
            table: "signin_challenges",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_signin_challenges_browser",
            schema: "identity",
            table: "signin_challenges",
            column: "browser");

        migrationBuilder.AddCheckConstraint(
            name: "ck_signin_challenges_browser",
            schema: "identity",
            table: "signin_challenges",
            sql: "browser IS NULL OR octet_length(browser) = 32");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropIndex(
            name: "ix_signin_challenges_browser",
            schema: "identity",
            table: "signin_challenges");

        migrationBuilder.DropCheckConstraint(
            name: "ck_signin_challenges_browser",
            schema: "identity",
            table: "signin_challenges");

        migrationBuilder.DropColumn(
            name: "browser",
            schema: "identity",
            table: "signin_challenges");

        migrationBuilder.DropColumn(
            name: "held_at",
            schema: "identity",
            table: "signin_challenges");
    }
}
