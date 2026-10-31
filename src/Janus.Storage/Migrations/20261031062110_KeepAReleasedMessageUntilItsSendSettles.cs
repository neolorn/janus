using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeepAReleasedMessageUntilItsSendSettles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<bool>(
            name: "released",
            schema: "identity",
            table: "sends",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "released",
            schema: "identity",
            table: "sends");
    }
}
