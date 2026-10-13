using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class RecordWhetherAMailboxPushWasAttempted : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // INT-MAIL-007, D-177: a removal of a mailbox no push of which was ever attempted
        // is confirmed unsent. Whether a row written before this was ever pushed cannot be
        // told from what it holds, so every such row counts as attempted and its removal
        // is sent; a row written after carries what the publisher records.
        migrationBuilder.AddColumn<bool>(
            name: "attempted",
            schema: "identity",
            table: "mailboxes",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AlterColumn<bool>(
            name: "attempted",
            schema: "identity",
            table: "mailboxes",
            type: "boolean",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "boolean",
            oldDefaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "attempted",
            schema: "identity",
            table: "mailboxes");
    }
}
