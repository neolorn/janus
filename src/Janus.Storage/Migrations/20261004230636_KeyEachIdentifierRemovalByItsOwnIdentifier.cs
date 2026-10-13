using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeyEachIdentifierRemovalByItsOwnIdentifier : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropPrimaryKey(
            name: "pk_identifier_removals",
            schema: "identity",
            table: "identifier_removals");

        migrationBuilder.AddColumn<Guid>(
            name: "removal_id",
            schema: "identity",
            table: "identifier_removals",
            type: "uuid",
            nullable: true);

        // REG-IDENT-006, CONV-DESIGN-004 (D-189): a row written before this was the one
        // removal of its identifier. It keeps that identifier as the one it came from and
        // takes an identifier of its own, a version 7 value made from the instant the row
        // carries, as the removal makes one for a new row.
        migrationBuilder.Sql(
            """
            UPDATE identity.identifier_removals SET removal_id = CAST(
                lpad(to_hex(CAST(floor(extract(epoch FROM removed_at) * 1000) AS bigint)), 12, '0')
                || '7' || substr(replace(CAST(gen_random_uuid() AS text), '-', ''), 14) AS uuid);
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "removal_id",
            schema: "identity",
            table: "identifier_removals",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AddPrimaryKey(
            name: "pk_identifier_removals",
            schema: "identity",
            table: "identifier_removals",
            column: "removal_id");

        migrationBuilder.CreateIndex(
            name: "ix_identifier_removals_identifier",
            schema: "identity",
            table: "identifier_removals",
            column: "identifier_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropPrimaryKey(
            name: "pk_identifier_removals",
            schema: "identity",
            table: "identifier_removals");

        migrationBuilder.DropIndex(
            name: "ix_identifier_removals_identifier",
            schema: "identity",
            table: "identifier_removals");

        migrationBuilder.DropColumn(
            name: "removal_id",
            schema: "identity",
            table: "identifier_removals");

        // REG-IDENT-006: the earlier key holds one removal an identifier, so it is refused
        // where an identifier stands behind a second; no row is removed to make room.
        migrationBuilder.AddPrimaryKey(
            name: "pk_identifier_removals",
            schema: "identity",
            table: "identifier_removals",
            column: "identifier_id");
    }
}
