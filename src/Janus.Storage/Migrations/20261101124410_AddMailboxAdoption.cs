using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
/// <remarks>
/// REG-MAIL-003 (D-255): whether a mailbox was reserved under <c>adopt</c>; every mailbox
/// reserved before was not.
/// </remarks>
internal sealed partial class AddMailboxAdoption : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<bool>(
            name: "may_adopt",
            schema: "identity",
            table: "mailboxes",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "may_adopt",
            schema: "identity",
            table: "mailboxes");
    }
}
