using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddChallengeLandingHandle : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<byte[]>(
            name: "landing_handle",
            schema: "identity",
            table: "signin_challenges",
            type: "bytea",
            maxLength: 32,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_signin_challenges_landing_handle",
            schema: "identity",
            table: "signin_challenges",
            column: "landing_handle",
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_signin_challenges_landing_handle",
            schema: "identity",
            table: "signin_challenges",
            sql: "landing_handle IS NULL OR octet_length(landing_handle) = 32");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropIndex(
            name: "ux_signin_challenges_landing_handle",
            schema: "identity",
            table: "signin_challenges");

        migrationBuilder.DropCheckConstraint(
            name: "ck_signin_challenges_landing_handle",
            schema: "identity",
            table: "signin_challenges");

        migrationBuilder.DropColumn(
            name: "landing_handle",
            schema: "identity",
            table: "signin_challenges");
    }
}
