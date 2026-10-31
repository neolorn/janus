using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class OpenARestoreWithoutAnIdentifier : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_signin_challenges_identifier",
            schema: "identity",
            table: "signin_challenges");

        migrationBuilder.AlterColumn<byte[]>(
            name: "identifier",
            schema: "identity",
            table: "signin_challenges",
            type: "bytea",
            maxLength: 32,
            nullable: true,
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldMaxLength: 32);

        migrationBuilder.AddCheckConstraint(
            name: "ck_signin_challenges_identifier",
            schema: "identity",
            table: "signin_challenges",
            sql: "identifier IS NULL OR octet_length(identifier) = 32");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // The version before opens no restore, so a restore in progress goes; it lapses
        // in minutes and the frontend begins it again.
        migrationBuilder.Sql(
            """
            DELETE FROM identity.signin_challenges WHERE identifier IS NULL;
            """);

        migrationBuilder.DropCheckConstraint(
            name: "ck_signin_challenges_identifier",
            schema: "identity",
            table: "signin_challenges");

        migrationBuilder.AlterColumn<byte[]>(
            name: "identifier",
            schema: "identity",
            table: "signin_challenges",
            type: "bytea",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldMaxLength: 32,
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_signin_challenges_identifier",
            schema: "identity",
            table: "signin_challenges",
            sql: "octet_length(identifier) = 32");
    }
}
