using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class HoldAnInvitationsErasedKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_invitations_key",
            schema: "identity",
            table: "invitations");

        // PRIV-RIGHT-005a AC14 (D-187): an invitation forgotten while a forgotten key was
        // stored as nothing takes the 32 zero bytes of an erased key, so one erased
        // value stands wherever a wrapped key is held. No key is lost: such a row held
        // none.
        migrationBuilder.Sql(
            """
            UPDATE identity.invitations
            SET wrapped_key = decode(repeat('00', 32), 'hex')
            WHERE wrapped_key IS NULL;
            """);

        migrationBuilder.AlterColumn<byte[]>(
            name: "wrapped_key",
            schema: "identity",
            table: "invitations",
            type: "bytea",
            nullable: false,
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_invitations_key",
            schema: "identity",
            table: "invitations",
            sql: "(enc_identifiers IS NULL) = (wrapped_key = decode(repeat('00', 32), 'hex'))");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_invitations_key",
            schema: "identity",
            table: "invitations");

        migrationBuilder.AlterColumn<byte[]>(
            name: "wrapped_key",
            schema: "identity",
            table: "invitations",
            type: "bytea",
            nullable: true,
            oldClrType: typeof(byte[]),
            oldType: "bytea");

        migrationBuilder.Sql(
            """
            UPDATE identity.invitations
            SET wrapped_key = NULL
            WHERE wrapped_key = decode(repeat('00', 32), 'hex');
            """);

        migrationBuilder.AddCheckConstraint(
            name: "ck_invitations_key",
            schema: "identity",
            table: "invitations",
            sql: "(enc_identifiers IS NULL) = (wrapped_key IS NULL)");
    }
}
