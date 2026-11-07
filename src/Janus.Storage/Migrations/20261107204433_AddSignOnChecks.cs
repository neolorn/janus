using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
/// <remarks>
/// BFF-SESS-006 (D-279): whether the sign-on a browser has in flight only checks, kept
/// with the rest of it; a sign-on started before this column was is not one.
/// </remarks>
internal sealed partial class AddSignOnChecks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<bool>(
            name: "signon_check",
            schema: "identity",
            table: "preauthentication_sessions",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "signon_check",
            schema: "identity",
            table: "preauthentication_sessions");
    }
}
