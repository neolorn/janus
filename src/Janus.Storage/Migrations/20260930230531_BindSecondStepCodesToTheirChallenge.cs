using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class BindSecondStepCodesToTheirChallenge : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTH-FACT-002 AC4: a second step's code answers the one sign-in or step-up it
        // was issued for.
        migrationBuilder.AddColumn<byte[]>(
            name: "challenge",
            schema: "identity",
            table: "signin_links",
            type: "bytea",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_signin_links_challenge",
            schema: "identity",
            table: "signin_links",
            sql: "challenge IS NULL OR octet_length(challenge) = 32");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_signin_links_challenge",
            schema: "identity",
            table: "signin_links");

        migrationBuilder.DropColumn(
            name: "challenge",
            schema: "identity",
            table: "signin_links");
    }
}
