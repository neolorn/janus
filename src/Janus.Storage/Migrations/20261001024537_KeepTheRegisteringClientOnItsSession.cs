using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeepTheRegisteringClientOnItsSession : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // REG-SESS-008, API-REDIR-002 (D-166, 145): the session a registration's terms
        // step establishes keeps the client the registration captured, and every other
        // session holds none. No existing row is rewritten.
        migrationBuilder.AddColumn<string>(
            name: "client",
            schema: "identity",
            table: "sessions",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "client",
            schema: "identity",
            table: "sessions");
    }
}
