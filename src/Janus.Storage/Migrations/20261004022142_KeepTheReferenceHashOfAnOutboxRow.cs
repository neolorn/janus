using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeepTheReferenceHashOfAnOutboxRow : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTH-ABUSE-004, PRIV-RIGHT-005a (D-183): a row whose key erasure has
        // overwritten is removed uncarried and its count released, so the row keeps the
        // hash of the reference it is counted under outside its encrypted content.
        migrationBuilder.AddColumn<byte[]>(
            name: "reference",
            schema: "identity",
            table: "send_outbox",
            type: "bytea",
            maxLength: 32,
            nullable: false,
            defaultValue: Array.Empty<byte>());

        // A row written before a send carried its reference holds none in its content,
        // which no statement can read. Its reference is its own identifier, as the 128
        // bits it is, in base64url without padding, and this is that reference's hash as
        // the ledger keeps one. Nothing was counted under it.
        migrationBuilder.Sql(
            """
            UPDATE identity.send_outbox
            SET reference = sha256(convert_to(translate(encode(uuid_send(id), 'base64'), '+/=', '-_'), 'UTF8'));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "reference",
            schema: "identity",
            table: "send_outbox");
    }
}
