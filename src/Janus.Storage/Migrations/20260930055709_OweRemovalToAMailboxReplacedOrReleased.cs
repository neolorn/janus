using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class OweRemovalToAMailboxReplacedOrReleased : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // INT-MAIL-006, D-178: a reservation released before this is owed its removal
        // from the instant it was released, as it was, and no longer stands for its
        // address.
        migrationBuilder.DropIndex(
            name: "ux_mailboxes_fingerprint",
            schema: "identity",
            table: "mailboxes");

        migrationBuilder.DropCheckConstraint(
            name: "ck_mailboxes_released",
            schema: "identity",
            table: "mailboxes");

        migrationBuilder.RenameColumn(
            name: "released_at",
            schema: "identity",
            table: "mailboxes",
            newName: "removal_owed_at");

        migrationBuilder.CreateIndex(
            name: "ux_mailboxes_fingerprint",
            schema: "identity",
            table: "mailboxes",
            column: "fingerprint",
            unique: true,
            filter: "fingerprint <> decode(repeat('00', 32), 'hex') AND removal_owed_at IS NULL");

        migrationBuilder.AddCheckConstraint(
            name: "ck_mailboxes_removal_owed",
            schema: "identity",
            table: "mailboxes",
            sql: "removal_owed_at IS NULL OR holder IS NULL OR retired_at IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropIndex(
            name: "ux_mailboxes_fingerprint",
            schema: "identity",
            table: "mailboxes");

        migrationBuilder.DropCheckConstraint(
            name: "ck_mailboxes_removal_owed",
            schema: "identity",
            table: "mailboxes");

        migrationBuilder.RenameColumn(
            name: "removal_owed_at",
            schema: "identity",
            table: "mailboxes",
            newName: "released_at");

        migrationBuilder.CreateIndex(
            name: "ux_mailboxes_fingerprint",
            schema: "identity",
            table: "mailboxes",
            column: "fingerprint",
            unique: true,
            filter: "fingerprint <> decode(repeat('00', 32), 'hex')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_mailboxes_released",
            schema: "identity",
            table: "mailboxes",
            sql: "released_at IS NULL OR (holder IS NULL AND retired_at IS NULL)");
    }
}
